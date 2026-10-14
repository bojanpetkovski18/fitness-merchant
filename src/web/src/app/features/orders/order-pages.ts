import { Dialog } from '@angular/cdk/dialog';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Page } from '../../core/catalog/catalog.models';
import { toApiProblem } from '../../core/http/api-problem';
import { Order, OrderSummary, orderStatusLabels } from '../../core/orders/order.models';
import { ToastService } from '../../core/ui/toast.service';
import { OrderStatusBadge } from '../../shared/orders/order-status-badge';
import { confirmAction } from '../../shared/ui/confirm-dialog';
import { Pagination } from '../../shared/ui/pagination';
import { EmptyState, ErrorState } from '../../shared/ui/states';

function isNotFound(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 404;
}

/** Items, totals and address, shared by the confirmation and detail pages. */
@Component({
  selector: 'app-order-summary',
  imports: [CurrencyPipe, RouterLink],
  template: `
    @let o = order();
    <div class="grid gap-6 lg:grid-cols-[1fr_20rem]">
      <section
        aria-labelledby="items-heading"
        class="rounded-xl border border-border bg-surface p-5"
      >
        <h2 id="items-heading" class="font-bold">Items</h2>
        <ul class="mt-3 divide-y divide-border text-sm">
          @for (item of o.items; track item.productId) {
            <li class="flex justify-between gap-3 py-2">
              <span class="min-w-0">
                {{ item.quantity }} ×
                @if (item.productAvailable) {
                  <a
                    [routerLink]="['/products', item.productSlug]"
                    class="font-semibold hover:text-accent"
                    >{{ item.productName }}</a
                  >
                } @else {
                  <span class="font-semibold">{{ item.productName }}</span>
                  <span class="text-muted"> (no longer sold)</span>
                }
                <span class="text-muted"> at {{ item.unitPrice | currency: 'EUR' }}</span>
              </span>
              <span class="shrink-0 font-semibold">{{ item.lineTotal | currency: 'EUR' }}</span>
            </li>
          }
        </ul>
        <dl class="mt-3 space-y-1 border-t border-border pt-3 text-sm">
          <div class="flex justify-between">
            <dt>Subtotal</dt>
            <dd>{{ o.subtotal | currency: 'EUR' }}</dd>
          </div>
          <div class="flex justify-between">
            <dt>Shipping</dt>
            <dd>{{ o.shipping === 0 ? 'Free' : (o.shipping | currency: 'EUR') }}</dd>
          </div>
          <div class="flex justify-between text-base font-black">
            <dt>Total</dt>
            <dd>{{ o.total | currency: 'EUR' }}</dd>
          </div>
        </dl>
      </section>
      <section
        aria-labelledby="delivery-heading"
        class="rounded-xl border border-border bg-surface p-5 text-sm"
      >
        <h2 id="delivery-heading" class="font-bold">Delivery</h2>
        @let a = o.shippingAddress;
        <p class="mt-2">
          {{ a.fullName }}<br />{{ a.line1 }}
          @if (a.line2) {
            <br />{{ a.line2 }}
          }
          <br />{{ a.postalCode }} {{ a.city }}<br />{{ a.country }}
        </p>
        <p class="mt-2 text-muted">{{ a.phone }} · {{ o.contactEmail }}</p>
        <h2 class="mt-4 font-bold">Payment</h2>
        <p class="mt-2">
          @if (o.payment.cardLast4) {
            {{ o.payment.cardBrand }} ending in {{ o.payment.cardLast4 }}
          } @else {
            Not paid
          }
        </p>
      </section>
    </div>
  `,
})
export class OrderSummaryView {
  readonly order = input.required<Order>();
}

@Component({
  selector: 'app-order-confirmation-page',
  imports: [RouterLink, OrderSummaryView, EmptyState, ErrorState],
  template: `
    <div class="mx-auto max-w-5xl px-4 py-8 md:py-10">
      @if (notFound()) {
        <app-empty-state
          title="Order not found"
          message="Check the link or find the order in your account."
        >
          <a
            routerLink="/account/orders"
            class="rounded-md bg-accent px-4 py-2 font-bold text-on-accent"
            >My orders</a
          >
        </app-empty-state>
      } @else if (order.error()) {
        <app-error-state title="Couldn't load your order" (retry)="order.reload()" />
      } @else if (order.hasValue()) {
        @let o = order.value();
        <div class="rounded-xl border border-success/40 bg-success/10 p-6">
          <p class="text-sm font-semibold uppercase tracking-widest text-success">
            Order confirmed
          </p>
          <h1 class="fm-display mt-2 text-3xl md:text-4xl">Thank you for your order</h1>
          <p class="mt-2 text-lg">
            Your order number is <strong class="font-mono">{{ o.number }}</strong
            >. We've sent nothing to {{ o.contactEmail }}: this is a demo store.
          </p>
        </div>
        <div class="mt-6"><app-order-summary [order]="o" /></div>
        <div class="mt-6 flex flex-wrap gap-3">
          <a
            [routerLink]="['/account/orders', o.number]"
            class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent"
            >View order</a
          >
          <a routerLink="/supplements" class="rounded-md border border-border px-5 py-3 font-bold"
            >Continue shopping</a
          >
        </div>
      } @else {
        <p role="status" class="text-muted">Loading your order…</p>
      }
    </div>
  `,
})
export class OrderConfirmationPage {
  readonly number = input.required<string>();
  protected readonly order = httpResource<Order>(
    () => `/api/orders/${encodeURIComponent(this.number())}`,
  );
  protected readonly notFound = computed(() => isNotFound(this.order.error()));
}

@Component({
  selector: 'app-orders-page',
  imports: [
    RouterLink,
    DatePipe,
    CurrencyPipe,
    OrderStatusBadge,
    Pagination,
    EmptyState,
    ErrorState,
  ],
  template: `
    <h1 class="fm-display text-3xl md:text-4xl">My orders</h1>
    <div class="mt-6">
      @if (orders.error()) {
        <app-error-state title="Couldn't load your orders" (retry)="orders.reload()" />
      } @else if (orders.hasValue()) {
        @if (orders.value().totalCount === 0) {
          <app-empty-state
            title="No orders yet"
            message="Your orders will appear here once you check out."
          >
            <a
              routerLink="/supplements"
              class="rounded-md bg-accent px-4 py-2 font-bold text-on-accent"
              >Start shopping</a
            >
          </app-empty-state>
        } @else {
          <ul class="divide-y divide-border rounded-xl border border-border bg-surface">
            @for (o of orders.value().items; track o.number) {
              <li
                class="relative flex flex-wrap items-center justify-between gap-3 p-4 hover:bg-surface-2"
              >
                <div>
                  <a
                    [routerLink]="['/account/orders', o.number]"
                    class="font-mono font-bold after:absolute after:inset-0 after:content-['']"
                  >
                    {{ o.number }}
                  </a>
                  <p class="text-sm text-muted">
                    {{ o.placedAt | date: 'mediumDate' }} · {{ o.itemCount }}
                    {{ o.itemCount === 1 ? 'item' : 'items' }}
                  </p>
                </div>
                <div class="flex items-center gap-3">
                  <app-order-status-badge [status]="o.status" />
                  <span class="font-bold">{{ o.total | currency: 'EUR' }}</span>
                </div>
              </li>
            }
          </ul>
          <app-pagination
            [page]="orders.value().page"
            [pageSize]="orders.value().pageSize"
            [totalCount]="orders.value().totalCount"
          />
        }
      } @else {
        <p role="status" class="text-muted">Loading your orders…</p>
      }
    </div>
  `,
})
export class OrdersPage {
  /** From the query string. */
  readonly page = input<string>();
  protected readonly orders = httpResource<Page<OrderSummary>>(
    () => `/api/orders?page=${Number(this.page()) || 1}`,
  );
}

@Component({
  selector: 'app-order-detail-page',
  imports: [RouterLink, DatePipe, OrderStatusBadge, OrderSummaryView, EmptyState, ErrorState],
  template: `
    <a routerLink="/account/orders" class="text-sm font-semibold text-muted hover:text-fg"
      >‹ My orders</a
    >
    @if (notFound()) {
      <div class="mt-4">
        <app-empty-state
          title="Order not found"
          message="It may belong to another account, or the link is wrong."
        />
      </div>
    } @else if (order.error()) {
      <div class="mt-4">
        <app-error-state title="Couldn't load this order" (retry)="order.reload()" />
      </div>
    } @else if (order.hasValue()) {
      @let o = order.value();
      <div class="mt-2 flex flex-wrap items-center gap-3">
        <h1 class="fm-display font-mono text-3xl md:text-4xl">{{ o.number }}</h1>
        <app-order-status-badge [status]="o.status" />
      </div>
      <p class="mt-1 text-muted">Placed {{ o.placedAt | date: 'medium' }}</p>

      <section aria-labelledby="timeline-heading" class="mt-6">
        <h2 id="timeline-heading" class="font-bold">Status</h2>
        <ol class="mt-3 space-y-3 border-l-2 border-border pl-4">
          @for (h of o.history; track $index) {
            <li class="relative">
              <span
                aria-hidden="true"
                class="absolute -left-[1.4rem] top-1.5 size-3 rounded-full bg-accent"
              ></span>
              <p class="font-semibold">{{ label(h.status) }}</p>
              <p class="text-sm text-muted">
                <time [attr.datetime]="h.at">{{ h.at | date: 'medium' }}</time>
                @if (h.note) {
                  · {{ h.note }}
                }
              </p>
            </li>
          }
        </ol>
      </section>

      <div class="mt-6"><app-order-summary [order]="o" /></div>

      @if (o.canCancel) {
        <button
          type="button"
          class="mt-6 rounded-md border border-danger px-5 py-2.5 font-bold text-danger hover:bg-danger hover:text-bg disabled:opacity-60"
          [disabled]="cancelling()"
          (click)="cancel(o.number)"
        >
          {{ cancelling() ? 'Cancelling…' : 'Cancel order' }}
        </button>
      }
    } @else {
      <p role="status" class="mt-4 text-muted">Loading the order…</p>
    }
  `,
})
export class OrderDetailPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  readonly number = input.required<string>();
  protected readonly order = httpResource<Order>(
    () => `/api/orders/${encodeURIComponent(this.number())}`,
  );
  protected readonly notFound = computed(() => isNotFound(this.order.error()));
  protected readonly cancelling = signal(false);

  protected label(status: Order['status']): string {
    return orderStatusLabels[status];
  }

  protected async cancel(number: string): Promise<void> {
    const confirmed = await confirmAction(this.dialog, {
      title: 'Cancel this order?',
      message: 'The items go back on sale. In this demo no real money was taken.',
      confirmLabel: 'Cancel order',
      dismissLabel: 'Keep order',
    });
    if (!confirmed) return;
    this.cancelling.set(true);
    try {
      await firstValueFrom(
        this.http.post<Order>(`/api/orders/${encodeURIComponent(number)}/cancel`, null),
      );
      this.toasts.show(`Order ${number} was cancelled.`);
      this.order.reload();
    } catch (error) {
      this.toasts.error(toApiProblem(error).title);
      this.order.reload();
    } finally {
      this.cancelling.set(false);
    }
  }
}
