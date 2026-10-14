import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { siteName } from '../../core/app-title-strategy';
import { BrandInfo, Page, ProductCard } from '../../core/catalog/catalog.models';
import { ProductGrid } from '../../shared/catalog/product-grid';
import { EmptyState, ErrorState, ProductGridSkeleton } from '../../shared/ui/states';

@Component({
  selector: 'app-brands-page',
  imports: [RouterLink, ErrorState],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 md:py-10">
      <h1 class="fm-display text-4xl md:text-5xl">Brands</h1>
      <p class="mt-2 text-muted">Every brand we stock, all invented for this demo store.</p>
      <div class="mt-8">
        @if (brands.error()) {
          <app-error-state title="Couldn't load brands" (retry)="brands.reload()" />
        } @else if (brands.hasValue()) {
          <ul class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            @for (brand of brands.value(); track brand.slug) {
              <li
                class="relative overflow-hidden rounded-xl border border-border bg-surface transition hover:border-accent focus-within:border-accent"
              >
                <img [src]="brand.logoUrl" alt="" width="600" height="200" class="w-full" />
                <div class="p-4">
                  <h2 class="font-bold">
                    <a
                      [routerLink]="['/brands', brand.slug]"
                      class="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none"
                      >{{ brand.name }}</a
                    >
                  </h2>
                  <p class="mt-1 text-sm text-muted">{{ brand.description }}</p>
                  <p class="mt-2 text-sm font-semibold">{{ brand.productCount }} products</p>
                </div>
              </li>
            }
          </ul>
        } @else {
          <p role="status" class="text-muted">Loading brands…</p>
        }
      </div>
    </div>
  `,
})
export class BrandsPage {
  protected readonly brands = httpResource<BrandInfo[]>(() => '/api/catalog/brands');
}

@Component({
  selector: 'app-brand-page',
  imports: [RouterLink, ProductGrid, ProductGridSkeleton, ErrorState, EmptyState],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 md:py-10">
      @if (notFound()) {
        <app-empty-state
          title="Brand not found"
          message="It may have been removed or the link is wrong."
        >
          <a routerLink="/brands" class="rounded-md bg-accent px-4 py-2 font-bold text-on-accent"
            >All brands</a
          >
        </app-empty-state>
      } @else if (brand.error()) {
        <app-error-state title="Couldn't load this brand" (retry)="brand.reload()" />
      } @else if (brand.hasValue()) {
        @let b = brand.value();
        <header class="grid gap-6 md:grid-cols-[20rem_1fr] md:items-center">
          <img
            [src]="b.logoUrl"
            [alt]="b.name + ' logo'"
            width="600"
            height="200"
            class="w-full rounded-xl"
          />
          <div>
            <h1 class="fm-display text-4xl">{{ b.name }}</h1>
            <p class="mt-2 max-w-2xl text-muted">{{ b.description }}</p>
          </div>
        </header>
        <section class="mt-10" aria-labelledby="brand-products-heading">
          <h2 id="brand-products-heading" class="fm-display mb-4 text-2xl">
            Products ({{ b.productCount }})
          </h2>
          @if (products.error()) {
            <app-error-state (retry)="products.reload()" />
          } @else if (products.hasValue()) {
            @if (products.value().items.length > 0) {
              <app-product-grid [products]="products.value().items" />
            } @else {
              <app-empty-state
                title="No products yet"
                message="This brand has nothing in stock right now."
              />
            }
          } @else {
            <app-product-grid-skeleton [count]="8" />
          }
        </section>
      } @else {
        <p role="status" class="text-muted">Loading brand…</p>
      }
    </div>
  `,
})
export class BrandPage {
  readonly slug = input.required<string>();

  protected readonly brand = httpResource<BrandInfo>(
    () => `/api/catalog/brands/${encodeURIComponent(this.slug())}`,
  );
  /** A brand has fewer products than the page size, so one page shows them all. */
  protected readonly products = httpResource<Page<ProductCard>>(
    () => `/api/catalog/products?brand=${encodeURIComponent(this.slug())}&pageSize=48`,
  );
  protected readonly notFound = computed(() => {
    const error = this.brand.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    const title = inject(Title);
    const meta = inject(Meta);
    effect(() => {
      const brand = this.brand.hasValue() ? this.brand.value() : undefined;
      if (brand) {
        title.setTitle(`${brand.name} · ${siteName}`);
        meta.updateTag({ name: 'description', content: `${brand.name}: ${brand.description}` });
      }
    });
  }
}
