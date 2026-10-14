import { Component, input, output } from '@angular/core';
import { BrandInfo, CategoryInfo } from '../../core/catalog/catalog.models';
import { ProductQuery, hasActiveFilters } from '../../core/catalog/product-query';

/**
 * Category, brand, price and availability filters. Presentational: it reports changes and the page writes
 * them to the URL, so the same panel works inline on desktop and inside the mobile drawer.
 */
@Component({
  selector: 'app-filter-panel',
  template: `
    @let q = query();
    <div class="space-y-6">
      <fieldset>
        <legend class="mb-2 text-sm font-bold uppercase tracking-wider text-muted">Category</legend>
        <ul class="space-y-1">
          @for (category of categories(); track category.slug) {
            <li>
              <label
                class="flex cursor-pointer items-center gap-2 rounded px-1 py-1.5 hover:bg-surface-2"
              >
                <input
                  type="checkbox"
                  class="size-4 accent-[var(--fm-accent)]"
                  [checked]="q.categories.includes(category.slug)"
                  (change)="toggle('categories', category.slug)"
                />
                {{ category.name }}
              </label>
            </li>
          }
        </ul>
      </fieldset>

      <fieldset>
        <legend class="mb-2 text-sm font-bold uppercase tracking-wider text-muted">Brand</legend>
        <ul class="space-y-1">
          @for (brand of brands(); track brand.slug) {
            <li>
              <label
                class="flex cursor-pointer items-center gap-2 rounded px-1 py-1.5 hover:bg-surface-2"
              >
                <input
                  type="checkbox"
                  class="size-4 accent-[var(--fm-accent)]"
                  [checked]="q.brands.includes(brand.slug)"
                  (change)="toggle('brands', brand.slug)"
                />
                {{ brand.name }}
              </label>
            </li>
          }
        </ul>
      </fieldset>

      <fieldset>
        <legend class="mb-2 text-sm font-bold uppercase tracking-wider text-muted">
          Price (€)
        </legend>
        <div class="flex items-center gap-2">
          <label class="sr-only" [for]="idPrefix() + 'min-price'">Minimum price in euros</label>
          <input
            [id]="idPrefix() + 'min-price'"
            type="number"
            inputmode="decimal"
            min="0"
            step="1"
            placeholder="Min"
            [value]="q.minPrice ?? ''"
            (change)="setPrice('minPrice', $event)"
            class="w-full rounded-md border border-border bg-surface px-3 py-2"
          />
          <span aria-hidden="true" class="text-muted">–</span>
          <label class="sr-only" [for]="idPrefix() + 'max-price'">Maximum price in euros</label>
          <input
            [id]="idPrefix() + 'max-price'"
            type="number"
            inputmode="decimal"
            min="0"
            step="1"
            placeholder="Max"
            [value]="q.maxPrice ?? ''"
            (change)="setPrice('maxPrice', $event)"
            class="w-full rounded-md border border-border bg-surface px-3 py-2"
          />
        </div>
      </fieldset>

      <fieldset>
        <legend class="sr-only">Availability</legend>
        <label
          class="flex cursor-pointer items-center gap-2 rounded px-1 py-1.5 hover:bg-surface-2"
        >
          <input
            type="checkbox"
            class="size-4 accent-[var(--fm-accent)]"
            [checked]="q.inStock"
            (change)="changed.emit({ inStock: !q.inStock })"
          />
          In stock only
        </label>
      </fieldset>

      @if (active()) {
        <button
          type="button"
          class="w-full rounded-md border border-border px-4 py-2 text-sm font-bold hover:bg-surface-2"
          (click)="cleared.emit()"
        >
          Clear all filters
        </button>
      }
    </div>
  `,
})
export class FilterPanel {
  readonly query = input.required<ProductQuery>();
  readonly categories = input.required<readonly CategoryInfo[]>();
  readonly brands = input.required<readonly BrandInfo[]>();
  /** Keeps label/input IDs unique when the panel is rendered twice (desktop + drawer). */
  readonly idPrefix = input('');
  readonly changed = output<Partial<ProductQuery>>();
  readonly cleared = output();

  protected active(): boolean {
    return hasActiveFilters({ ...this.query(), q: '' });
  }

  protected toggle(key: 'categories' | 'brands', slug: string): void {
    const current = this.query()[key];
    const next = current.includes(slug) ? current.filter((s) => s !== slug) : [...current, slug];
    this.changed.emit({ [key]: next });
  }

  protected setPrice(key: 'minPrice' | 'maxPrice', event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const value = raw === '' ? null : Number(raw);
    this.changed.emit({
      [key]: value !== null && Number.isFinite(value) && value >= 0 ? value : null,
    });
  }
}
