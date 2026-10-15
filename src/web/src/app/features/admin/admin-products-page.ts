import { Dialog } from '@angular/cdk/dialog';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AdminBrand,
  AdminCategory,
  AdminProductRow,
  AdminProductSortKey,
  DeleteOutcome,
  SortDirection,
  departmentLabels,
} from '../../core/admin/admin.models';
import { Page } from '../../core/catalog/catalog.models';
import { toApiProblem } from '../../core/http/api-problem';
import { ToastService } from '../../core/ui/toast.service';
import { confirmAction } from '../../shared/ui/confirm-dialog';
import { Pagination } from '../../shared/ui/pagination';
import { EmptyState, ErrorState } from '../../shared/ui/states';

interface Column {
  readonly key: AdminProductSortKey;
  readonly label: string;
  readonly align: 'left' | 'right';
}

const defaultSort = 'updated-desc';

/** Server-side paged, searched, filtered and sorted product table; all state lives in the URL. */
@Component({
  selector: 'app-admin-products-page',
  imports: [RouterLink, CurrencyPipe, DatePipe, Pagination, EmptyState, ErrorState],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <h1 class="fm-display text-3xl md:text-4xl">Products</h1>
      <a
        routerLink="/admin/products/new"
        class="rounded-md bg-accent px-4 py-2.5 font-bold text-on-accent hover:bg-accent-strong"
        >New product</a
      >
    </div>

    <form
      role="search"
      aria-label="Filter products"
      class="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_12rem_12rem_10rem]"
      (submit)="$event.preventDefault()"
    >
      <div>
        <label for="admin-q" class="block text-sm font-semibold">Search</label>
        <input
          id="admin-q"
          #searchBox
          type="search"
          placeholder="Name or SKU"
          [value]="q() ?? ''"
          (input)="search(searchBox.value)"
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg"
        />
      </div>
      <div>
        <label for="admin-category" class="block text-sm font-semibold">Category</label>
        <select
          id="admin-category"
          #categoryBox
          [value]="category() ?? ''"
          (change)="filter('category', categoryBox.value)"
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg"
        >
          <option value="">All categories</option>
          @if (categories.hasValue()) {
            @for (group of categoryGroups(); track group.label) {
              <optgroup [label]="group.label">
                @for (c of group.items; track c.id) {
                  <option [value]="c.id" [selected]="c.id === category()">{{ c.name }}</option>
                }
              </optgroup>
            }
          }
        </select>
      </div>
      <div>
        <label for="admin-brand" class="block text-sm font-semibold">Brand</label>
        <select
          id="admin-brand"
          #brandBox
          [value]="brand() ?? ''"
          (change)="filter('brand', brandBox.value)"
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg"
        >
          <option value="">All brands</option>
          @if (brands.hasValue()) {
            @for (b of brands.value(); track b.id) {
              <option [value]="b.id" [selected]="b.id === brand()">{{ b.name }}</option>
            }
          }
        </select>
      </div>
      <div>
        <label for="admin-status" class="block text-sm font-semibold">Status</label>
        <select
          id="admin-status"
          #statusBox
          [value]="status() ?? ''"
          (change)="filter('status', statusBox.value)"
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg"
        >
          <option value="">All</option>
          <option value="active" [selected]="status() === 'active'">In store</option>
          <option value="archived" [selected]="status() === 'archived'">Archived</option>
        </select>
      </div>
    </form>

    <div class="mt-6">
      @if (products.error()) {
        <app-error-state title="Couldn't load products" (retry)="products.reload()" />
      } @else if (products.hasValue()) {
        @let page = products.value();
        <p role="status" class="mb-3 text-sm text-muted">
          {{ page.totalCount }} {{ page.totalCount === 1 ? 'product' : 'products' }}
          @if (products.isLoading()) {
            · updating…
          }
        </p>
        @if (page.totalCount === 0) {
          <app-empty-state
            title="No products match"
            message="Try another search, or clear the filters."
          >
            <a
              routerLink="/admin/products"
              class="rounded-md border border-border px-4 py-2 font-bold hover:bg-surface-2"
              >Clear filters</a
            >
          </app-empty-state>
        } @else {
          <!-- Tablet and desktop: a sortable table. -->
          <div class="hidden overflow-x-auto rounded-xl border border-border md:block">
            <table class="w-full text-left text-sm">
              <caption class="sr-only">
                Products, sorted by
                {{
                  sortLabel()
                }}
              </caption>
              <thead class="bg-surface-2 text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th scope="col" class="w-14 px-3 py-2"><span class="sr-only">Image</span></th>
                  @for (col of columns; track col.key) {
                    <th
                      scope="col"
                      [attr.aria-sort]="ariaSort(col.key)"
                      [class.text-right]="col.align === 'right'"
                      class="px-3 py-2"
                    >
                      <button
                        type="button"
                        class="inline-flex items-center gap-1 font-semibold uppercase hover:text-fg"
                        (click)="sortBy(col.key)"
                      >
                        {{ col.label }}
                        <span aria-hidden="true">{{ sortArrow(col.key) }}</span>
                      </button>
                    </th>
                  }
                  <th scope="col" class="px-3 py-2">Status</th>
                  <th scope="col" class="px-3 py-2 text-right">
                    <span class="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border bg-surface">
                @for (p of page.items; track p.id) {
                  <tr>
                    <td class="px-3 py-2">
                      @if (p.imageUrl) {
                        <img
                          [src]="p.imageUrl"
                          alt=""
                          width="40"
                          height="40"
                          class="size-10 rounded bg-surface-2 object-contain"
                        />
                      }
                    </td>
                    <td class="px-3 py-2">
                      <a
                        [routerLink]="['/admin/products', p.id]"
                        class="font-semibold hover:text-accent"
                        >{{ p.name }}</a
                      >
                      <p class="text-xs text-muted">{{ p.brandName }} · {{ p.categoryName }}</p>
                    </td>
                    <td class="px-3 py-2 font-mono text-xs">{{ p.sku }}</td>
                    <td class="px-3 py-2 text-right">
                      {{ p.salePrice | currency: 'EUR' }}
                      @if (p.discountPercent > 0) {
                        <p class="text-xs text-muted">−{{ p.discountPercent }} %</p>
                      }
                    </td>
                    <td class="px-3 py-2 text-right" [class.text-danger]="p.stockQuantity === 0">
                      {{ p.stockQuantity }}
                    </td>
                    <td class="px-3 py-2 text-xs text-muted">
                      <time [attr.datetime]="p.updatedAt">{{
                        p.updatedAt | date: 'mediumDate'
                      }}</time>
                    </td>
                    <td class="px-3 py-2">
                      <span
                        class="rounded-full px-2 py-0.5 text-xs font-semibold"
                        [class]="
                          p.isActive ? 'bg-success/15 text-success' : 'bg-surface-2 text-muted'
                        "
                        >{{ p.isActive ? 'In store' : 'Archived' }}</span
                      >
                    </td>
                    <td class="px-3 py-2 text-right whitespace-nowrap">
                      <a
                        [routerLink]="['/admin/products', p.id]"
                        class="rounded px-2 py-1 font-semibold hover:bg-surface-2"
                        >Edit<span class="sr-only"> {{ p.name }}</span></a
                      >
                      <button
                        type="button"
                        class="rounded px-2 py-1 font-semibold text-danger hover:bg-danger/10"
                        (click)="remove(p)"
                      >
                        Delete<span class="sr-only"> {{ p.name }}</span>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Phones: stacked cards with the same data and actions. -->
          <div class="md:hidden">
            <label for="admin-sort" class="block text-sm font-semibold">Sort by</label>
            <select
              id="admin-sort"
              #sortBox
              (change)="setSort(sortBox.value)"
              class="mt-1 mb-3 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg"
            >
              @for (option of sortOptions; track option.value) {
                <option [value]="option.value" [selected]="option.value === currentSort()">
                  {{ option.label }}
                </option>
              }
            </select>
            <ul class="space-y-3">
              @for (p of page.items; track p.id) {
                <li class="flex gap-3 rounded-xl border border-border bg-surface p-3">
                  @if (p.imageUrl) {
                    <img
                      [src]="p.imageUrl"
                      alt=""
                      width="56"
                      height="56"
                      class="size-14 shrink-0 rounded bg-surface-2 object-contain"
                    />
                  }
                  <div class="min-w-0 flex-1">
                    <a
                      [routerLink]="['/admin/products', p.id]"
                      class="font-semibold break-words hover:text-accent"
                      >{{ p.name }}</a
                    >
                    <p class="text-xs text-muted">
                      <span class="font-mono">{{ p.sku }}</span> · {{ p.brandName }}
                    </p>
                    <p class="mt-1 text-sm">
                      {{ p.salePrice | currency: 'EUR' }} · {{ p.stockQuantity }} in stock ·
                      <span [class]="p.isActive ? 'text-success' : 'text-muted'">{{
                        p.isActive ? 'In store' : 'Archived'
                      }}</span>
                    </p>
                    <div class="mt-2 flex gap-2">
                      <a
                        [routerLink]="['/admin/products', p.id]"
                        class="rounded-md border border-border px-3 py-1 text-sm font-semibold"
                        >Edit<span class="sr-only"> {{ p.name }}</span></a
                      >
                      <button
                        type="button"
                        class="rounded-md border border-danger/50 px-3 py-1 text-sm font-semibold text-danger"
                        (click)="remove(p)"
                      >
                        Delete<span class="sr-only"> {{ p.name }}</span>
                      </button>
                    </div>
                  </div>
                </li>
              }
            </ul>
          </div>

          <app-pagination
            [page]="page.page"
            [pageSize]="page.pageSize"
            [totalCount]="page.totalCount"
          />
        }
      } @else {
        <p role="status" class="text-muted">Loading products…</p>
      }
    </div>
  `,
})
export class AdminProductsPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  // Query parameters (component input binding).
  readonly q = input<string>();
  readonly category = input<string>();
  readonly brand = input<string>();
  readonly status = input<string>();
  readonly sort = input<string>();
  readonly page = input<string>();

  protected readonly columns: readonly Column[] = [
    { key: 'name', label: 'Name', align: 'left' },
    { key: 'sku', label: 'SKU', align: 'left' },
    { key: 'price', label: 'Price', align: 'right' },
    { key: 'stock', label: 'Stock', align: 'right' },
    { key: 'updated', label: 'Updated', align: 'left' },
  ];

  protected readonly sortOptions = [
    { value: 'updated-desc', label: 'Recently updated' },
    { value: 'name-asc', label: 'Name A–Z' },
    { value: 'name-desc', label: 'Name Z–A' },
    { value: 'price-asc', label: 'Price, low to high' },
    { value: 'price-desc', label: 'Price, high to low' },
    { value: 'stock-asc', label: 'Stock, lowest first' },
    { value: 'sku-asc', label: 'SKU' },
  ] as const;

  protected readonly categories = httpResource<AdminCategory[]>(() => '/api/admin/categories');
  protected readonly brands = httpResource<AdminBrand[]>(() => '/api/admin/brands');

  protected readonly currentSort = computed(() => this.sort() || defaultSort);

  protected readonly products = httpResource<Page<AdminProductRow>>(() => ({
    url: '/api/admin/products',
    params: {
      ...(this.q() ? { q: this.q()! } : {}),
      ...(this.category() ? { category: this.category()! } : {}),
      ...(this.brand() ? { brand: this.brand()! } : {}),
      ...(this.status() ? { status: this.status()! } : {}),
      sort: this.currentSort(),
      page: Number(this.page()) || 1,
    },
  }));

  protected readonly categoryGroups = computed(() => {
    const all = this.categories.hasValue() ? this.categories.value() : [];
    return (['supplements', 'gear'] as const).map((d) => ({
      label: departmentLabels[d],
      items: all.filter((c) => c.department === d),
    }));
  });

  protected readonly sortLabel = computed(
    () => this.sortOptions.find((o) => o.value === this.currentSort())?.label ?? 'Recently updated',
  );

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected search(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.navigate({ q: value.trim() || null }), 300);
  }

  protected filter(name: 'category' | 'brand' | 'status', value: string): void {
    this.navigate({ [name]: value || null });
  }

  protected setSort(value: string): void {
    this.navigate({ sort: value === defaultSort ? null : value });
  }

  protected sortBy(key: AdminProductSortKey): void {
    const [currentKey, currentDir] = this.currentSort().split('-') as [string, SortDirection];
    const dir: SortDirection =
      currentKey === key
        ? currentDir === 'asc'
          ? 'desc'
          : 'asc'
        : key === 'updated'
          ? 'desc'
          : 'asc';
    this.setSort(`${key}-${dir}`);
  }

  protected ariaSort(key: AdminProductSortKey): 'ascending' | 'descending' | null {
    const [currentKey, dir] = this.currentSort().split('-');
    return currentKey === key ? (dir === 'asc' ? 'ascending' : 'descending') : null;
  }

  protected sortArrow(key: AdminProductSortKey): string {
    const sort = this.ariaSort(key);
    return sort === 'ascending' ? '▲' : sort === 'descending' ? '▼' : '';
  }

  protected async remove(product: AdminProductRow): Promise<void> {
    const confirmed = await confirmAction(this.dialog, {
      title: `Delete “${product.name}”?`,
      message:
        'If it was ever ordered it is archived instead: it leaves the store but stays on past orders.',
      confirmLabel: 'Delete',
      dismissLabel: 'Keep it',
    });
    if (!confirmed) return;
    try {
      const result = await firstValueFrom(
        this.http.delete<{ outcome: DeleteOutcome }>(`/api/admin/products/${product.id}`),
      );
      this.toasts.show(
        result.outcome === 'archived'
          ? `“${product.name}” has orders, so it was archived: it is no longer in the store.`
          : `“${product.name}” was deleted.`,
      );
    } catch (error) {
      this.toasts.error(toApiProblem(error).title);
    }
    this.products.reload();
  }

  private navigate(params: Record<string, string | null>): void {
    void this.router.navigate([], {
      queryParams: { ...params, page: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
