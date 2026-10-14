import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { toApiProblem } from '../http/api-problem';
import { ToastService } from '../ui/toast.service';
import { CartItemInput, CartLine, CartView, emptyCart, maxLineQuantity } from './cart.models';
import { guestCartStorage } from './guest-cart-storage';

export type CartStatus = 'loading' | 'ready' | 'error';

/**
 * The cart, shared by the header badge, product pages and the cart page (D015, D006).
 * Two backends behind one interface:
 * - guests: IDs and quantities in localStorage, priced by the server's quote endpoint;
 * - customers: the server cart, with optimistic quantity changes rolled back on error.
 * Signing in merges the guest cart into the server cart; signing out leaves an empty guest cart.
 */
@Injectable({ providedIn: 'root' })
export class CartStore {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly toasts = inject(ToastService);

  private readonly state = signal<CartView>(emptyCart);
  private readonly loadStatus = signal<CartStatus>('loading');
  private readonly busy = signal<ReadonlySet<string>>(new Set());

  readonly cart = this.state.asReadonly();
  readonly status = this.loadStatus.asReadonly();
  readonly itemCount = computed(() => this.state().itemCount);
  readonly hasBlockingIssues = computed(() => this.state().lines.some((l) => l.issue !== null));

  constructor() {
    let previousUserId: string | null | undefined;
    effect(() => {
      const userId = this.session.user()?.id ?? null;
      untracked(() => {
        if (previousUserId === undefined || userId === previousUserId) {
          if (previousUserId === undefined) void this.load();
        } else if (userId !== null && previousUserId === null) {
          void this.mergeGuestCart();
        } else {
          // Signed out, or a different account: start from that state's own cart.
          void this.load();
        }
        previousUserId = userId;
      });
    });
  }

  isBusy(productId: string): boolean {
    return this.busy().has(productId);
  }

  quantityOf(productId: string): number {
    return this.state().lines.find((l) => l.productId === productId)?.quantity ?? 0;
  }

  async load(): Promise<void> {
    this.loadStatus.set('loading');
    try {
      this.state.set(
        this.session.isSignedIn()
          ? await this.fetchServerCart()
          : await this.quote(guestCartStorage.read()),
      );
      this.loadStatus.set('ready');
    } catch {
      this.loadStatus.set('error');
    }
  }

  /** Adds to whatever is already in the cart for that product. */
  async add(
    product: { readonly id: string; readonly name: string },
    quantity = 1,
  ): Promise<boolean> {
    const desired = Math.min(this.quantityOf(product.id) + quantity, maxLineQuantity);
    const ok = await this.setQuantity(product.id, desired);
    if (ok) {
      this.toasts.show(`Added ${product.name} to your cart.`, {
        action: { label: 'View cart', link: '/cart' },
      });
    }
    return ok;
  }

  async setQuantity(productId: string, quantity: number): Promise<boolean> {
    const clamped = Math.max(1, Math.min(Math.trunc(quantity), maxLineQuantity));
    return this.mutate(productId, {
      guest: (items) => upsert(items, productId, clamped),
      optimistic: (cart) => withQuantity(cart, productId, clamped),
      server: () =>
        firstValueFrom(
          this.http.put<CartView>(`/api/cart/items/${productId}`, { quantity: clamped }),
        ),
    });
  }

  async remove(productId: string): Promise<boolean> {
    return this.mutate(productId, {
      guest: (items) => items.filter((i) => i.productId !== productId),
      optimistic: (cart) => ({
        ...cart,
        lines: cart.lines.filter((l) => l.productId !== productId),
      }),
      server: () => firstValueFrom(this.http.delete<CartView>(`/api/cart/items/${productId}`)),
    });
  }

  async clear(): Promise<boolean> {
    return this.mutate('*', {
      guest: () => [],
      optimistic: () => emptyCart,
      server: () => firstValueFrom(this.http.delete<CartView>('/api/cart')),
    });
  }

  private async mutate(
    key: string,
    change: {
      guest: (items: CartItemInput[]) => CartItemInput[];
      optimistic: (cart: CartView) => CartView;
      server: () => Promise<CartView>;
    },
  ): Promise<boolean> {
    const previous = this.state();
    const previousGuestItems = guestCartStorage.read();
    this.setBusy(key, true);
    try {
      if (this.session.isSignedIn()) {
        this.state.set(change.optimistic(previous));
        this.state.set(await change.server());
      } else {
        const items = change.guest(previousGuestItems);
        guestCartStorage.write(items);
        this.state.set(this.reconcileGuest(await this.quote(items)));
      }
      this.loadStatus.set('ready');
      return true;
    } catch (error) {
      // Roll back: the view, and for guests the stored items too.
      this.state.set(previous);
      if (!this.session.isSignedIn()) {
        guestCartStorage.write(previousGuestItems);
      }
      this.toasts.error(this.describe(error));
      return false;
    } finally {
      this.setBusy(key, false);
    }
  }

  private async mergeGuestCart(): Promise<void> {
    const items = guestCartStorage.read();
    if (items.length === 0) {
      await this.load();
      return;
    }
    try {
      this.state.set(await firstValueFrom(this.http.post<CartView>('/api/cart/merge', { items })));
      guestCartStorage.clear();
      this.loadStatus.set('ready');
    } catch {
      // Keep the guest items; the next successful sign-in or load will try again.
      await this.load();
    }
  }

  private fetchServerCart(): Promise<CartView> {
    return firstValueFrom(this.http.get<CartView>('/api/cart'));
  }

  private quote(items: readonly CartItemInput[]): Promise<CartView> {
    return items.length === 0
      ? Promise.resolve(emptyCart)
      : firstValueFrom(this.http.post<CartView>('/api/cart/quote', { items }));
  }

  /** The server knows the stock: shrink guest lines that ask for more than exists, and say so. */
  private reconcileGuest(cart: CartView): CartView {
    const tooMany = cart.lines.filter((l) => l.issue === 'exceeds-stock');
    if (tooMany.length > 0) {
      const items = guestCartStorage.read().map((item) => {
        const line = tooMany.find((l) => l.productId === item.productId);
        return line ? { ...item, quantity: line.maxQuantity } : item;
      });
      guestCartStorage.write(items);
      for (const line of tooMany) {
        this.toasts.error(`Only ${line.maxQuantity} of ${line.name} available.`);
      }
      return {
        ...cart,
        lines: cart.lines.map((l) =>
          l.issue === 'exceeds-stock' ? { ...l, quantity: l.maxQuantity, issue: null } : l,
        ),
      };
    }
    return cart;
  }

  private describe(error: unknown): string {
    if (error instanceof HttpErrorResponse && (error.status === 409 || error.status === 404)) {
      return toApiProblem(error).title;
    }
    return "Couldn't update your cart. Please try again.";
  }

  private setBusy(key: string, value: boolean): void {
    this.busy.update((set) => {
      const next = new Set(set);
      if (value) next.add(key);
      else next.delete(key);
      return next;
    });
  }
}

function upsert(items: CartItemInput[], productId: string, quantity: number): CartItemInput[] {
  return items.some((i) => i.productId === productId)
    ? items.map((i) => (i.productId === productId ? { ...i, quantity } : i))
    : [...items, { productId, quantity }];
}

/** Optimistic view: only quantities change; money stays as last priced until the server answers. */
function withQuantity(cart: CartView, productId: string, quantity: number): CartView {
  const lines: CartLine[] = cart.lines.map((l) =>
    l.productId === productId ? { ...l, quantity } : l,
  );
  const delta =
    quantity - (cart.lines.find((l) => l.productId === productId)?.quantity ?? quantity);
  return { ...cart, lines, itemCount: Math.max(0, cart.itemCount + delta) };
}
