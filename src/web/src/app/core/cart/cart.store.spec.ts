import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionStore, SessionUser } from '../auth/session.store';
import { ToastService } from '../ui/toast.service';
import { CartLine, CartView, emptyCart } from './cart.models';
import { CartStore } from './cart.store';
import { guestCartStorage } from './guest-cart-storage';

const productId = '0199a000-0000-7000-8000-0000000000aa';
const otherId = '0199a000-0000-7000-8000-0000000000bb';
const customer: SessionUser = {
  id: 'u1',
  email: 'c@test.example',
  displayName: 'C',
  roles: ['Customer'],
};

function line(overrides: Partial<CartLine> = {}): CartLine {
  return {
    productId,
    slug: 'whey',
    name: 'Whey Isolate',
    brandName: 'Halvard Nutrition',
    imageUrl: null,
    price: 10,
    discountPercent: 0,
    unitPrice: 10,
    quantity: 1,
    maxQuantity: 10,
    lineTotal: 10,
    issue: null,
    ...overrides,
  };
}

function cartOf(...lines: CartLine[]): CartView {
  const subtotal = lines.reduce((sum, l) => sum + l.lineTotal, 0);
  return {
    ...emptyCart,
    lines,
    itemCount: lines.reduce((n, l) => n + l.quantity, 0),
    subtotal,
    total: subtotal + 4.9,
    shipping: 4.9,
  };
}

describe('CartStore', () => {
  let store: CartStore;
  let backend: HttpTestingController;
  let session: SessionStore;
  let toasts: ToastService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    toasts = TestBed.inject(ToastService);
    store = TestBed.inject(CartStore);
    TestBed.tick(); // initial load as a guest with an empty cart: no request
  });

  afterEach(() => backend.verify());

  async function signIn(user: SessionUser | null): Promise<void> {
    const loading = session.load();
    backend.expectOne('/api/auth/me').flush({ user });
    await loading;
    TestBed.tick();
  }

  it('guest: adding stores only IDs and quantities and prices the cart on the server', async () => {
    const adding = store.add({ id: productId, name: 'Whey Isolate' });
    const quote = backend.expectOne('/api/cart/quote');
    expect(quote.request.body).toEqual({ items: [{ productId, quantity: 1 }] });
    quote.flush(cartOf(line()));
    expect(await adding).toBe(true);

    expect(JSON.parse(localStorage.getItem('fm-cart')!)).toEqual([{ productId, quantity: 1 }]);
    expect(store.itemCount()).toBe(1);
    expect(toasts.toasts().at(-1)?.message).toBe('Added Whey Isolate to your cart.');
  });

  it('guest: a line asking for more than stock is shrunk to what exists, with a message', async () => {
    guestCartStorage.write([{ productId, quantity: 2 }]);

    const setting = store.setQuantity(productId, 5);
    backend
      .expectOne('/api/cart/quote')
      .flush(cartOf(line({ quantity: 5, maxQuantity: 3, issue: 'exceeds-stock', lineTotal: 30 })));
    await setting;

    expect(guestCartStorage.read()).toEqual([{ productId, quantity: 3 }]);
    expect(store.cart().lines[0]).toMatchObject({ quantity: 3, issue: null });
    expect(
      toasts
        .toasts()
        .some((t) => t.tone === 'error' && t.message === 'Only 3 of Whey Isolate available.'),
    ).toBe(true);
  });

  it('signing in merges the guest cart into the server cart and clears guest storage', async () => {
    guestCartStorage.write([
      { productId, quantity: 2 },
      { productId: otherId, quantity: 1 },
    ]);

    await signIn(customer);
    const merge = backend.expectOne('/api/cart/merge');
    expect(merge.request.body).toEqual({
      items: [
        { productId, quantity: 2 },
        { productId: otherId, quantity: 1 },
      ],
    });
    merge.flush(cartOf(line({ quantity: 2, lineTotal: 20 })));
    await Promise.resolve();
    await Promise.resolve();

    expect(guestCartStorage.read()).toEqual([]);
    expect(store.itemCount()).toBe(2);
  });

  it('signing in with an empty guest cart just loads the server cart', async () => {
    await signIn(customer);
    backend.expectOne('/api/cart').flush(cartOf(line()));
    await Promise.resolve();

    expect(store.itemCount()).toBe(1);
  });

  it('signing out leaves an empty guest cart', async () => {
    await signIn(customer);
    backend.expectOne('/api/cart').flush(cartOf(line()));
    await Promise.resolve();

    const signingOut = session.signOut();
    backend.expectOne('/api/auth/sign-out').flush(null);
    await signingOut;
    TestBed.tick();
    await Promise.resolve();

    expect(store.itemCount()).toBe(0);
    expect(store.cart().lines).toEqual([]);
  });

  it('customer: quantity changes are optimistic and rolled back with the reason on error', async () => {
    await signIn(customer);
    backend.expectOne('/api/cart').flush(cartOf(line({ quantity: 1 })));
    await Promise.resolve();

    const setting = store.setQuantity(productId, 2);
    expect(store.cart().lines[0]!.quantity).toBe(2); // shown before the server answers
    expect(store.isBusy(productId)).toBe(true);

    backend
      .expectOne(`/api/cart/items/${productId}`)
      .flush({ title: 'Only 1 available.', status: 409 }, { status: 409, statusText: 'Conflict' });
    expect(await setting).toBe(false);

    expect(store.cart().lines[0]!.quantity).toBe(1);
    expect(store.isBusy(productId)).toBe(false);
    expect(toasts.toasts().at(-1)).toMatchObject({ tone: 'error', message: 'Only 1 available.' });
  });

  it('guest: a failed quote restores both the view and the stored items', async () => {
    guestCartStorage.write([{ productId, quantity: 1 }]);

    const adding = store.add({ id: otherId, name: 'Creatine' });
    backend.expectOne('/api/cart/quote').error(new ProgressEvent('error'));
    expect(await adding).toBe(false);

    expect(guestCartStorage.read()).toEqual([{ productId, quantity: 1 }]);
    expect(toasts.toasts().at(-1)?.tone).toBe('error');
  });
});
