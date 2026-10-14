import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, linkedSignal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { siteName } from '../../core/app-title-strategy';
import { CartStore } from '../../core/cart/cart.store';
import { ProductCard, ProductDetail } from '../../core/catalog/catalog.models';
import { PriceTag } from '../../shared/catalog/price-tag';
import { ProductGrid } from '../../shared/catalog/product-grid';
import { RatingStars } from '../../shared/catalog/rating-stars';
import { StockBadge } from '../../shared/catalog/stock-badge';
import { EmptyState, ErrorState, ProductGridSkeleton } from '../../shared/ui/states';

@Component({
  selector: 'app-product-page',
  imports: [
    RouterLink,
    PriceTag,
    RatingStars,
    StockBadge,
    ProductGrid,
    ProductGridSkeleton,
    ErrorState,
    EmptyState,
  ],
  templateUrl: './product-page.html',
})
export class ProductPage {
  /** From the route parameter (withComponentInputBinding). */
  readonly slug = input.required<string>();

  protected readonly product = httpResource<ProductDetail>(
    () => `/api/catalog/products/${encodeURIComponent(this.slug())}`,
  );
  protected readonly related = httpResource<ProductCard[]>(
    () => `/api/catalog/products/${encodeURIComponent(this.slug())}/related`,
  );

  protected readonly notFound = computed(() => {
    const error = this.product.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected readonly cart = inject(CartStore);

  /** Resets to 1 whenever another product is shown. */
  protected readonly quantity = linkedSignal({ source: this.slug, computation: () => 1 });

  constructor() {
    const title = inject(Title);
    const meta = inject(Meta);
    effect(() => {
      const product = this.product.hasValue() ? this.product.value() : undefined;
      if (product) {
        title.setTitle(`${product.name} · ${siteName}`);
        meta.updateTag({
          name: 'description',
          content: `${product.name} by ${product.brand.name}. ${product.description}`,
        });
      } else if (this.notFound()) {
        title.setTitle(`Product not found · ${siteName}`);
      }
    });
  }

  protected async addToCart(product: ProductDetail): Promise<void> {
    if (await this.cart.add(product, this.quantity())) {
      this.quantity.set(1);
    }
  }

  protected setQuantity(value: number, max: number): void {
    this.quantity.set(Math.min(Math.max(1, Math.trunc(value) || 1), Math.max(1, max)));
  }
}
