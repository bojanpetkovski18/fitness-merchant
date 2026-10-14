import { httpResource } from '@angular/common/http';
import { Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandInfo, Page, ProductCard } from '../../core/catalog/catalog.models';
import { ProductGrid } from '../../shared/catalog/product-grid';
import { ErrorState, ProductGridSkeleton } from '../../shared/ui/states';

/** One product rail per section; each loads and fails on its own so one bad request never blanks the page. */
function rail(sort: string) {
  return httpResource<Page<ProductCard>>(() => `/api/catalog/products?sort=${sort}&pageSize=4`);
}

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, ProductGrid, ProductGridSkeleton, ErrorState],
  templateUrl: './home-page.html',
})
export class HomePage {
  protected readonly deals = rail('discount');
  protected readonly newArrivals = rail('newest');
  protected readonly topRated = rail('rating');
  protected readonly brands = httpResource<BrandInfo[]>(() => '/api/catalog/brands');

  /** "Top rated" is hidden until products actually have reviews (M11), rather than ranking unrated items. */
  protected readonly hasRatings = computed(() =>
    (this.topRated.hasValue() ? this.topRated.value().items : []).some((p) => p.ratingCount > 0),
  );

  /** Hero imagery from the deals rail; empty (not an error) if that request fails. */
  protected readonly heroItems = computed(() =>
    this.deals.hasValue() ? this.deals.value().items : [],
  );

  protected readonly rails = [
    { id: 'deals', title: 'Hot deals', resource: this.deals },
    { id: 'new', title: 'New arrivals', resource: this.newArrivals },
  ];
}
