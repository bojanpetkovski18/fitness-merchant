import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../core/cart/cart.store';
import { ProductCard as ProductCardModel } from '../../core/catalog/catalog.models';
import { PriceTag } from './price-tag';
import { RatingStars } from './rating-stars';
import { StockBadge } from './stock-badge';
import { BusyButton } from '../ui/busy-button';

/**
 * One accessible link per card: the product name is the link, stretched over the card with ::after, so the
 * whole card is clickable but screen readers hear one link instead of a card full of them.
 */
@Component({
  selector: 'app-product-card',
  imports: [RouterLink, PriceTag, RatingStars, StockBadge, BusyButton],
  template: `
    @let p = product();
    <article
      class="group relative flex h-full flex-col overflow-hidden rounded-xl border border-border bg-surface transition hover:border-accent focus-within:border-accent"
    >
      <div class="relative">
        <img
          [src]="p.imageUrl"
          alt=""
          width="800"
          height="800"
          [attr.loading]="priority() ? 'eager' : 'lazy'"
          [attr.fetchpriority]="priority() ? 'high' : null"
          decoding="async"
          class="aspect-square w-full object-cover"
          [class.opacity-60]="p.stockStatus === 'out-of-stock'"
        />
        @if (p.discountPercent > 0) {
          <span
            class="absolute right-2 top-2 rounded-md bg-danger px-2 py-1 text-xs font-black text-bg"
          >
            -{{ p.discountPercent }}%<span class="sr-only"> off</span>
          </span>
        }
      </div>
      <div class="flex flex-1 flex-col gap-1.5 p-3 md:p-4">
        <p class="text-xs font-semibold uppercase tracking-wider text-muted">{{ p.brand.name }}</p>
        <h3 class="font-bold leading-snug">
          <a
            [routerLink]="['/products', p.slug]"
            class="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none"
            >{{ p.name }}</a
          >
        </h3>
        <app-rating-stars [average]="p.ratingAverage" [count]="p.ratingCount" />
        <div class="mt-auto flex flex-wrap items-end justify-between gap-2 pt-2">
          <app-price-tag
            [price]="p.price"
            [salePrice]="p.salePrice"
            [discountPercent]="p.discountPercent"
          />
          <app-stock-badge [status]="p.stockStatus" />
        </div>
        <!-- Above the stretched link (z-10), so it is its own click and tab target. -->
        <button
          type="button"
          class="relative z-10 mt-2 w-full rounded-md border border-accent px-3 py-2 text-sm font-bold text-accent hover:bg-accent hover:text-on-accent disabled:border-border disabled:text-muted disabled:hover:bg-transparent"
          [disabled]="p.stockStatus === 'out-of-stock'"
          [appBusy]="cart.isBusy(p.id)"
          [attr.aria-label]="
            p.stockStatus === 'out-of-stock'
              ? p.name + ' is sold out'
              : 'Add ' + p.name + ' to cart'
          "
          (click)="cart.add(p)"
        >
          {{
            p.stockStatus === 'out-of-stock'
              ? 'Sold out'
              : cart.isBusy(p.id)
                ? 'Adding…'
                : 'Add to cart'
          }}
        </button>
      </div>
    </article>
  `,
})
export class ProductCard {
  readonly product = input.required<ProductCardModel>();
  /** First-row cards are likely the Largest Contentful Paint, so they load eagerly instead of lazily. */
  readonly priority = input(false);
  protected readonly cart = inject(CartStore);
}
