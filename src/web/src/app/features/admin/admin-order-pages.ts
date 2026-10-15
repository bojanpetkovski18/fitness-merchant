import { DIALOG_DATA, Dialog, DialogRef } from '@angular/cdk/dialog';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AdminOrder,
  AdminOrderRow,
  lastDays,
  transitionLabels,
} from '../../core/admin/admin.models';
import { Page } from '../../core/catalog/catalog.models';
import { toApiProblem } from '../../core/http/api-problem';
import { OrderStatus, orderStatusLabels } from '../../core/orders/order.models';
import { ToastService } from '../../core/ui/toast.service';
import { OrderStatusBadge } from '../../shared/orders/order-status-badge';
import { Pagination } from '../../shared/ui/pagination';
import { EmptyState, ErrorState } from '../../shared/ui/states';
import { OrderSummaryView } from '../orders/order-pages';

const fieldClass = 'mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg';

/** A blob error body (export) still carries problem details; read them so the toast says what went wrong. */
async function problemTitle(error: unknown): Promise<string> {
  if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
    try {
      const body = JSON.parse(await error.error.text()) as { title?: unknown };
      if (typeof body.title === 'string') return body.title;
    } catch {
      // not JSON: fall through to the generic message
    }
  }
  return toApiProblem(error).title;
}

@Component({
  selector: 'app-admin-orders-page',
  imports: [
    RouterLink,
    CurrencyPipe,
    DatePipe,
    OrderStatusBadge,
    Pagination,
    EmptyState,
    ErrorState,
  ],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <h1 class="fm-display text-3xl md:text-4xl">Orders</h1>
      <div class="text-right">
        <button
          type="button"
          class="rounded-md border border-border px-4 py-2.5 font-bold hover:bg-surface-2 disabled:opacity-60"
          [disabled]="exporting()"
          [attr.aria-describedby]="'export-hint'"
          (click)="export()"
        >
          {{ exporting() ? 'Exporting…' : 'Export CSV' }}
        </button>
        <p id="export-hint" class="mt-1 text-xs text-muted">
          Orders placed {{ exportRange().from }} to {{ exportRange().to }} (UTC)
        </p>
      </div>
    </div>

    <form
      role="search"
      aria-label="Filter orders"
      class="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_11rem_10rem_10rem]"
      (submit)="$event.preventDefault()"
    >
      <div>
        <label for="order-q" class="block text-sm font-semibold">Search</label>
        <input
          id="order-q"
          #searchBox
          type="search"
          placeholder="Order number or email"
          [value]="q() ?? ''"
          (input)="search(searchBox.value)"
          [class]="fieldClass"
        />
      </div>
      <div>
        <label for="order-status" class="block text-sm font-semibold">Status</label>
        <select
          id="order-status"
          #statusBox
          (change)="filter('status', statusBox.value)"
          [class]="fieldClass"
        >
          <option value="">All statuses</option>
          @for (s of statuses; track s) {
            <option [value]="s" [selected]="s === status()">{{ statusLabel(s) }}</option>
          }
        </select>
      </div>
      <div>
        <label for="order-from" class="block text-sm font-semibold">From</label>
        <input
          id="order-from"
          #fromBox
          type="date"
          [value]="from() ?? ''"
          (change)="filter('from', fromBox.value)"
          [class]="fieldClass"
        />
      </div>
      <div>
        <label for="order-to" class="block text-sm font-semibold">To</label>
        <input
          id="order-to"
          #toBox
          type="date"
          [value]="to() ?? ''"
          (change)="filter('to', toBox.value)"
          [class]="fieldClass"
        />
      </div>
    </form>

    <div class="mt-6">
      @if (filterError(); as message) {
        <p role="alert" class="mb-3 text-sm font-semibold text-danger">{{ message }}</p>
      }
      @if (orders.error() && !filterError()) {
        <app-error-state title="Couldn't load orders" (retry)="orders.reload()" />
      } @else if (shown(); as page) {
        <p role="status" class="mb-3 text-sm text-muted">
          {{ page.totalCount }} {{ page.totalCount === 1 ? 'order' : 'orders' }}
          @if (orders.isLoading()) {
            · updating…
          }
        </p>
        @if (page.totalCount === 0) {
          <app-empty-state title="No orders match" message="Try another search, status or dates.">
            <a
              routerLink="/admin/orders"
              class="rounded-md border border-border px-4 py-2 font-bold hover:bg-surface-2"
              >Clear filters</a
            >
          </app-empty-state>
        } @else {
          <div class="relative hidden overflow-x-auto rounded-xl border border-border md:block">
            <table class="w-full text-left text-sm">
              <caption class="sr-only">
                Orders, newest first
              </caption>
              <thead class="bg-surface-2 text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th scope="col" class="px-3 py-2">Order</th>
                  <th scope="col" class="px-3 py-2">Placed</th>
                  <th scope="col" class="px-3 py-2">Customer</th>
                  <th scope="col" class="px-3 py-2 text-right">Items</th>
                  <th scope="col" class="px-3 py-2 text-right">Total</th>
                  <th scope="col" class="px-3 py-2">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border bg-surface">
                @for (o of page.items; track o.number) {
                  <tr>
                    <td class="px-3 py-2">
                      <a
                        [routerLink]="['/admin/orders', o.number]"
                        class="font-mono font-bold hover:text-accent"
                        >{{ o.number }}</a
                      >
                    </td>
                    <td class="px-3 py-2 whitespace-nowrap">
                      <time [attr.datetime]="o.placedAt">{{ o.placedAt | date: 'medium' }}</time>
                    </td>
                    <td class="px-3 py-2">
                      {{ o.customerName }}
                      <p class="text-xs text-muted break-all">{{ o.contactEmail }}</p>
                    </td>
                    <td class="px-3 py-2 text-right">{{ o.itemCount }}</td>
                    <td class="px-3 py-2 text-right">{{ o.total | currency: 'EUR' }}</td>
                    <td class="px-3 py-2"><app-order-status-badge [status]="o.status" /></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <ul class="space-y-3 md:hidden">
            @for (o of page.items; track o.number) {
              <li class="relative rounded-xl border border-border bg-surface p-3">
                <div class="flex items-center justify-between gap-2">
                  <a
                    [routerLink]="['/admin/orders', o.number]"
                    class="font-mono font-bold after:absolute after:inset-0 after:content-['']"
                    >{{ o.number }}</a
                  >
                  <app-order-status-badge [status]="o.status" />
                </div>
                <p class="mt-1 text-sm">{{ o.customerName }} · {{ o.total | currency: 'EUR' }}</p>
                <p class="text-xs text-muted">
                  {{ o.placedAt | date: 'medium' }} · {{ o.itemCount }}
                  {{ o.itemCount === 1 ? 'item' : 'items' }}
                </p>
              </li>
            }
          </ul>
          <app-pagination
            [page]="page.page"
            [pageSize]="page.pageSize"
            [totalCount]="page.totalCount"
          />
        }
      } @else {
        <p role="status" class="text-muted">Loading orders…</p>
      }
    </div>
  `,
})
export class AdminOrdersPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly q = input<string>();
  readonly status = input<string>();
  readonly from = input<string>();
  readonly to = input<string>();
  readonly page = input<string>();

  protected readonly fieldClass = fieldClass;
  protected readonly statuses = Object.keys(orderStatusLabels) as OrderStatus[];
  protected readonly exporting = signal(false);

  protected readonly orders = httpResource<Page<AdminOrderRow>>(() => ({
    url: '/api/admin/orders',
    params: {
      ...(this.q() ? { q: this.q()! } : {}),
      ...(this.status() ? { status: this.status()! } : {}),
      ...(this.from() ? { from: this.from()! } : {}),
      ...(this.to() ? { to: this.to()! } : {}),
      page: Number(this.page()) || 1,
    },
  }));

  /** The last page received stays on screen while the next loads (see AdminProductsPage). */
  protected readonly shown = linkedSignal<
    Page<AdminOrderRow> | undefined,
    Page<AdminOrderRow> | undefined
  >({
    source: () => (this.orders.hasValue() ? this.orders.value() : undefined),
    computation: (next, previous) => next ?? previous?.value,
  });

  /** A 400 here means the filters are invalid (e.g. "from" after "to"): say which instead of "couldn't load". */
  protected readonly filterError = computed(() => {
    const error = this.orders.error();
    if (!(error instanceof HttpErrorResponse) || error.status !== 400) return null;
    const problem = toApiProblem(error);
    return Object.values(problem.fieldErrors).flat()[0] ?? problem.title;
  });

  /** The filter dates when both are set; otherwise the last 30 days. */
  protected readonly exportRange = computed(() =>
    this.from() && this.to() ? { from: this.from()!, to: this.to()! } : lastDays(30),
  );

  protected statusLabel(status: OrderStatus): string {
    return orderStatusLabels[status];
  }

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected search(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.navigate({ q: value.trim() || null }), 300);
  }

  protected filter(name: 'status' | 'from' | 'to', value: string): void {
    this.navigate({ [name]: value || null });
  }

  protected async export(): Promise<void> {
    const range = this.exportRange();
    this.exporting.set(true);
    try {
      const blob = await firstValueFrom(
        this.http.get('/api/admin/orders/export', { params: range, responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `orders-${range.from}-to-${range.to}.csv`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      this.toasts.show('The CSV export was downloaded.');
    } catch (error) {
      this.toasts.error(await problemTitle(error));
    } finally {
      this.exporting.set(false);
    }
  }

  private navigate(params: Record<string, string | null>): void {
    void this.router.navigate([], {
      queryParams: { ...params, page: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}

interface StatusDialogData {
  readonly number: string;
  readonly to: OrderStatus;
}

/** Confirms a status change and takes an optional note (e.g. a tracking number) for the history. */
@Component({
  selector: 'app-status-change-dialog',
  template: `
    <form
      class="w-[min(92vw,28rem)] rounded-xl border border-border bg-surface p-6 text-fg shadow-xl"
      (submit)="$event.preventDefault(); confirm()"
    >
      <h2 id="status-dialog-title" class="fm-display text-xl">{{ title }}</h2>
      <p id="status-dialog-message" class="mt-2 text-muted">{{ message }}</p>
      <label for="status-note" class="mt-4 block text-sm font-semibold">Note (optional)</label>
      <textarea
        id="status-note"
        #noteBox
        rows="3"
        maxlength="500"
        aria-describedby="status-note-hint"
        class="mt-1 block w-full rounded-md border border-border bg-bg px-3 py-2 text-fg"
        (input)="note.set(noteBox.value)"
      ></textarea>
      <p id="status-note-hint" class="mt-1 text-xs text-muted">
        Shown in the order history, for example a tracking number.
      </p>
      <div class="mt-6 flex justify-end gap-3">
        <button
          type="button"
          class="rounded-md border border-border px-4 py-2 font-bold hover:bg-surface-2"
          (click)="ref.close()"
        >
          {{ data.to === 'cancelled' ? 'Keep order' : 'Back' }}
        </button>
        <button
          type="submit"
          class="rounded-md px-4 py-2 font-bold"
          [class]="data.to === 'cancelled' ? 'bg-danger text-bg' : 'bg-accent text-on-accent'"
        >
          {{ label }}
        </button>
      </div>
    </form>
  `,
})
export class StatusChangeDialog {
  protected readonly data = inject<StatusDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<{ note: string }>>(DialogRef);
  protected readonly note = signal('');
  protected readonly label = transitionLabels[this.data.to];
  protected readonly title = `${this.label}: ${this.data.number}?`;
  protected readonly message =
    this.data.to === 'cancelled'
      ? 'The items go back into stock. The customer sees the order as cancelled.'
      : `The order becomes “${orderStatusLabels[this.data.to]}” and the customer sees it in their order history.`;

  protected confirm(): void {
    this.ref.close({ note: this.note() });
  }
}

@Component({
  selector: 'app-admin-order-page',
  imports: [RouterLink, DatePipe, OrderStatusBadge, OrderSummaryView, EmptyState, ErrorState],
  template: `
    <a routerLink="/admin/orders" class="text-sm font-semibold text-muted hover:text-fg"
      >‹ Orders</a
    >
    @if (notFound()) {
      <div class="mt-4"><app-empty-state title="Order not found" /></div>
    } @else if (order.error()) {
      <div class="mt-4">
        <app-error-state title="Couldn't load this order" (retry)="order.reload()" />
      </div>
    } @else if (order.hasValue()) {
      @let o = order.value();
      <div class="mt-2 flex flex-wrap items-center gap-3">
        <h1 class="fm-display font-mono text-3xl md:text-4xl">{{ o.order.number }}</h1>
        <app-order-status-badge [status]="o.order.status" />
      </div>
      <p class="mt-1 text-muted">
        Placed {{ o.order.placedAt | date: 'medium' }} by {{ o.customer.displayName }}
        <span class="break-all">({{ o.customer.email }})</span>
      </p>

      <section aria-labelledby="actions-heading" class="mt-6">
        <h2 id="actions-heading" class="font-bold">Actions</h2>
        @if (o.allowedTransitions.length === 0) {
          <p class="mt-2 text-sm text-muted">
            This order is {{ statusLabel(o.order.status).toLowerCase() }}; nothing more can change.
          </p>
        } @else {
          <div class="mt-2 flex flex-wrap gap-2">
            @for (to of o.allowedTransitions; track to) {
              <button
                type="button"
                class="rounded-md px-4 py-2 font-bold disabled:opacity-60"
                [class]="
                  to === 'cancelled'
                    ? 'border border-danger text-danger hover:bg-danger hover:text-bg'
                    : 'bg-accent text-on-accent hover:bg-accent-strong'
                "
                [disabled]="pending()"
                (click)="change(o.order.number, to)"
              >
                {{ transitionLabel(to) }}
              </button>
            }
          </div>
        }
      </section>

      <section aria-labelledby="history-heading" class="mt-6">
        <h2 id="history-heading" class="font-bold">History</h2>
        <ol class="mt-3 space-y-3 border-l-2 border-border pl-4">
          @for (h of o.history; track $index) {
            <li class="relative">
              <span
                aria-hidden="true"
                class="absolute top-1.5 -left-[1.4rem] size-3 rounded-full bg-accent"
              ></span>
              <p class="font-semibold">{{ statusLabel(h.toStatus) }}</p>
              <p class="text-sm text-muted">
                <time [attr.datetime]="h.at">{{ h.at | date: 'medium' }}</time>
                @if (h.changedBy) {
                  · by {{ h.changedBy }}
                }
                @if (h.note) {
                  · {{ h.note }}
                }
              </p>
            </li>
          }
        </ol>
      </section>

      <div class="mt-6"><app-order-summary [order]="o.order" /></div>
    } @else {
      <p role="status" class="mt-4 text-muted">Loading the order…</p>
    }
  `,
})
export class AdminOrderPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  readonly number = input.required<string>();

  protected readonly order = httpResource<AdminOrder>(
    () => `/api/admin/orders/${encodeURIComponent(this.number())}`,
  );
  protected readonly pending = signal(false);
  protected readonly notFound = computed(() => {
    const error = this.order.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected statusLabel(status: OrderStatus): string {
    return orderStatusLabels[status];
  }

  protected transitionLabel(status: OrderStatus): string {
    return transitionLabels[status];
  }

  protected async change(number: string, to: OrderStatus): Promise<void> {
    const ref = this.dialog.open<{ note: string }>(StatusChangeDialog, {
      data: { number, to } satisfies StatusDialogData,
      role: to === 'cancelled' ? 'alertdialog' : 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'status-dialog-title',
      ariaDescribedBy: 'status-dialog-message',
      autoFocus: '#status-note',
    });
    const result = await firstValueFrom(ref.closed);
    if (!result) return;

    this.pending.set(true);
    try {
      const updated = await firstValueFrom(
        this.http.post<AdminOrder>(`/api/admin/orders/${encodeURIComponent(number)}/status`, {
          toStatus: to,
          note: result.note || null,
        }),
      );
      this.order.set(updated);
      this.toasts.show(
        `${number} is now ${orderStatusLabels[updated.order.status].toLowerCase()}.`,
      );
    } catch (error) {
      // 409: someone else moved it first; the reload shows what is allowed now.
      this.toasts.error(toApiProblem(error).title);
      this.order.reload();
    } finally {
      this.pending.set(false);
    }
  }
}
