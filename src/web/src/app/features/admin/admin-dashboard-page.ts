import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Dashboard, lastDays } from '../../core/admin/admin.models';
import { toApiProblem } from '../../core/http/api-problem';
import { ErrorState } from '../../shared/ui/states';

const presets = [7, 30, 90] as const;
type Preset = (typeof presets)[number];

const euro = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });

/**
 * Sales for a date range (SPEC F15). The chart is a plain SVG drawn from the data (no chart library, D027): it is
 * an image with a summary label for screen readers, and the same numbers are in a table right below it.
 */
@Component({
  selector: 'app-admin-dashboard-page',
  imports: [RouterLink, CurrencyPipe, DatePipe, ErrorState],
  template: `
    <h1 class="fm-display text-3xl md:text-4xl">Dashboard</h1>

    <div class="mt-5 flex flex-wrap items-end gap-3" role="group" aria-label="Date range">
      @for (p of presets; track p) {
        <button
          type="button"
          class="rounded-md border px-3 py-2 text-sm font-bold"
          [class]="
            activePreset() === p
              ? 'border-accent bg-accent text-on-accent'
              : 'border-border hover:bg-surface-2'
          "
          [attr.aria-pressed]="activePreset() === p"
          (click)="choosePreset(p)"
        >
          Last {{ p }} days
        </button>
      }
      <form
        class="flex flex-wrap items-end gap-2"
        (submit)="$event.preventDefault(); applyCustom(fromBox.value, toBox.value)"
      >
        <div>
          <label for="dash-from" class="block text-xs font-semibold">From</label>
          <input
            id="dash-from"
            #fromBox
            type="date"
            [value]="range().from"
            class="mt-1 rounded-md border border-border bg-surface px-2 py-1.5 text-sm text-fg"
          />
        </div>
        <div>
          <label for="dash-to" class="block text-xs font-semibold">To</label>
          <input
            id="dash-to"
            #toBox
            type="date"
            [value]="range().to"
            class="mt-1 rounded-md border border-border bg-surface px-2 py-1.5 text-sm text-fg"
          />
        </div>
        <button
          type="submit"
          class="rounded-md border px-3 py-2 text-sm font-bold"
          [class]="
            activePreset() === null
              ? 'border-accent bg-accent text-on-accent'
              : 'border-border hover:bg-surface-2'
          "
          [attr.aria-pressed]="activePreset() === null"
        >
          Apply dates
        </button>
      </form>
    </div>
    <p class="mt-2 text-xs text-muted">
      Days are UTC. Revenue counts paid, shipped and delivered orders (shipping included) by the day
      they were placed.
    </p>

    <div class="mt-6">
      @if (rangeError(); as message) {
        <p
          role="alert"
          class="rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm font-semibold text-danger"
        >
          {{ message }}
        </p>
      } @else if (dashboard.error()) {
        <app-error-state title="Couldn't load the dashboard" (retry)="dashboard.reload()" />
      } @else if (shown(); as d) {
        <p role="status" class="sr-only">
          {{
            dashboard.isLoading()
              ? 'Updating…'
              : 'Showing ' + (d.from | date: 'mediumDate') + ' to ' + (d.to | date: 'mediumDate')
          }}
        </p>
        <dl class="grid gap-3 sm:grid-cols-3" [class.opacity-60]="dashboard.isLoading()">
          <div class="rounded-xl border border-border bg-surface p-4">
            <dt class="text-sm text-muted">Revenue</dt>
            <dd class="mt-1 text-2xl font-black">{{ d.revenue | currency: 'EUR' }}</dd>
          </div>
          <div class="rounded-xl border border-border bg-surface p-4">
            <dt class="text-sm text-muted">Orders</dt>
            <dd class="mt-1 text-2xl font-black">{{ d.orderCount }}</dd>
          </div>
          <div class="rounded-xl border border-border bg-surface p-4">
            <dt class="text-sm text-muted">Average order value</dt>
            <dd class="mt-1 text-2xl font-black">{{ d.averageOrderValue | currency: 'EUR' }}</dd>
          </div>
        </dl>

        <section
          aria-labelledby="chart-heading"
          class="mt-6 rounded-xl border border-border bg-surface p-4"
        >
          <h2 id="chart-heading" class="font-bold">Revenue per day</h2>
          @if (d.orderCount === 0) {
            <p class="mt-3 text-sm text-muted">No sales in this range.</p>
          } @else {
            <div class="mt-3 flex gap-2">
              <div
                class="flex flex-col justify-between text-right text-xs text-muted"
                aria-hidden="true"
              >
                <span>{{ chart().max | currency: 'EUR' : 'symbol' : '1.0-0' }}</span>
                <span>€0</span>
              </div>
              <svg
                role="img"
                [attr.aria-label]="chart().summary"
                [attr.viewBox]="'0 0 ' + chart().width + ' 100'"
                preserveAspectRatio="none"
                class="h-48 w-full border-b border-l border-border"
              >
                @for (bar of chart().bars; track bar.day) {
                  <rect
                    [attr.x]="bar.x"
                    [attr.y]="100 - bar.height"
                    [attr.width]="bar.width"
                    [attr.height]="bar.height"
                    class="fill-accent"
                  >
                    <title>{{ bar.title }}</title>
                  </rect>
                }
              </svg>
            </div>
            <div class="mt-1 flex justify-between pl-10 text-xs text-muted" aria-hidden="true">
              <span>{{ d.from | date: 'd MMM' }}</span>
              <span>{{ d.to | date: 'd MMM' }}</span>
            </div>
          }
          <details class="mt-4">
            <summary class="cursor-pointer text-sm font-semibold">
              Revenue per day as a table
            </summary>
            <div class="mt-2 max-h-80 overflow-y-auto">
              <table class="w-full text-left text-sm">
                <caption class="sr-only">
                  Revenue and orders per day
                </caption>
                <thead class="text-xs uppercase text-muted">
                  <tr>
                    <th scope="col" class="py-1 pr-3">Day</th>
                    <th scope="col" class="py-1 pr-3 text-right">Orders</th>
                    <th scope="col" class="py-1 text-right">Revenue</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of d.revenueByDay; track row.day) {
                    <tr class="border-t border-border">
                      <th scope="row" class="py-1 pr-3 font-normal">
                        {{ row.day | date: 'EEE d MMM y' }}
                      </th>
                      <td class="py-1 pr-3 text-right">{{ row.orders }}</td>
                      <td class="py-1 text-right">{{ row.revenue | currency: 'EUR' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </details>
        </section>

        <div class="mt-6 grid gap-6 lg:grid-cols-2">
          <section
            aria-labelledby="top-heading"
            class="rounded-xl border border-border bg-surface p-4"
          >
            <h2 id="top-heading" class="font-bold">Top products</h2>
            @if (d.topProducts.length === 0) {
              <p class="mt-3 text-sm text-muted">No products sold in this range.</p>
            } @else {
              <table class="mt-3 w-full text-left text-sm">
                <caption class="sr-only">
                  Top
                  {{
                    d.topProducts.length
                  }}
                  products by units sold
                </caption>
                <thead class="text-xs uppercase text-muted">
                  <tr>
                    <th scope="col" class="py-1 pr-3">Product</th>
                    <th scope="col" class="py-1 pr-3 text-right">Units</th>
                    <th scope="col" class="py-1 text-right">Revenue</th>
                  </tr>
                </thead>
                <tbody>
                  @for (p of d.topProducts; track p.productId) {
                    <tr class="border-t border-border">
                      <th scope="row" class="py-2 pr-3 font-normal">
                        <a
                          [routerLink]="['/admin/products', p.productId]"
                          class="font-semibold hover:text-accent"
                          >{{ p.name }}</a
                        >
                        <span class="block font-mono text-xs text-muted">{{ p.sku }}</span>
                      </th>
                      <td class="py-2 pr-3 text-right">{{ p.quantity }}</td>
                      <td class="py-2 text-right">{{ p.revenue | currency: 'EUR' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </section>

          <section
            aria-labelledby="low-heading"
            class="rounded-xl border border-border bg-surface p-4"
          >
            <h2 id="low-heading" class="font-bold">
              Low stock <span class="font-normal text-muted">(5 or fewer)</span>
            </h2>
            @if (d.lowStock.length === 0) {
              <p class="mt-3 text-sm text-muted">Nothing is running low.</p>
            } @else {
              <ul class="mt-3 divide-y divide-border text-sm">
                @for (p of d.lowStock; track p.productId) {
                  <li class="flex items-center justify-between gap-3 py-2">
                    <a
                      [routerLink]="['/admin/products', p.productId]"
                      class="min-w-0 font-semibold hover:text-accent"
                      >{{ p.name }}</a
                    >
                    <span
                      class="shrink-0"
                      [class.font-bold]="p.stockQuantity === 0"
                      [class.text-danger]="p.stockQuantity === 0"
                    >
                      {{ p.stockQuantity === 0 ? 'Sold out' : p.stockQuantity + ' left' }}
                    </span>
                  </li>
                }
              </ul>
            }
          </section>
        </div>
      } @else {
        <p role="status" class="text-muted">Loading the dashboard…</p>
      }
    </div>
  `,
})
export class AdminDashboardPage {
  private readonly router = inject(Router);

  // Query parameters: ?days=7|30|90, or ?from=…&to=… for a custom range.
  readonly days = input<string>();
  readonly from = input<string>();
  readonly to = input<string>();

  protected readonly presets = presets;

  protected readonly activePreset = computed<Preset | null>(() => {
    if (this.from() && this.to()) return null;
    const days = Number(this.days());
    return (presets as readonly number[]).includes(days) ? (days as Preset) : 30;
  });

  protected readonly range = computed(() => {
    const preset = this.activePreset();
    return preset === null ? { from: this.from()!, to: this.to()! } : lastDays(preset);
  });

  protected readonly dashboard = httpResource<Dashboard>(() => ({
    url: '/api/admin/dashboard',
    params: this.range(),
  }));

  protected readonly shown = linkedSignal<Dashboard | undefined, Dashboard | undefined>({
    source: () => (this.dashboard.hasValue() ? this.dashboard.value() : undefined),
    computation: (next, previous) => next ?? previous?.value,
  });

  protected readonly rangeError = computed(() => {
    const error = this.dashboard.error();
    if (!(error instanceof HttpErrorResponse) || error.status !== 400) return null;
    const problem = toApiProblem(error);
    return Object.values(problem.fieldErrors).flat()[0] ?? problem.title;
  });

  /** Bars in a 100-unit-high view box, one 10-unit slot per day; heights are relative to the best day. */
  protected readonly chart = computed(() => {
    const days = this.shown()?.revenueByDay ?? [];
    const max = Math.max(...days.map((d) => d.revenue), 0);
    const best = days.find((d) => d.revenue === max);
    return {
      max,
      width: Math.max(days.length, 1) * 10,
      bars: days.map((d, i) => ({
        day: d.day,
        x: i * 10 + 1,
        width: 8,
        height: max > 0 ? Math.max((d.revenue / max) * 100, d.revenue > 0 ? 1 : 0) : 0,
        title: `${d.day}: ${euro.format(d.revenue)}, ${d.orders} ${d.orders === 1 ? 'order' : 'orders'}`,
      })),
      summary: best
        ? `Revenue per day over ${days.length} days. Highest: ${euro.format(max)} on ${best.day}. The table below lists every day.`
        : 'No revenue in this range.',
    };
  });

  protected choosePreset(days: Preset): void {
    void this.router.navigate([], {
      queryParams: { days: days === 30 ? null : days, from: null, to: null },
    });
  }

  protected applyCustom(from: string, to: string): void {
    if (!from || !to) return;
    void this.router.navigate([], { queryParams: { days: null, from, to } });
  }
}
