import { Component, input } from '@angular/core';
import { ProductCard as ProductCardModel } from '../../core/catalog/catalog.models';
import { ProductCard } from './product-card';

export const productGridClass = 'grid grid-cols-2 gap-3 md:grid-cols-3 md:gap-4 lg:grid-cols-4';

@Component({
  selector: 'app-product-grid',
  imports: [ProductCard],
  template: `
    <ul [class]="gridClass">
      @for (product of products(); track product.id) {
        <li><app-product-card [product]="product" [priority]="$index < priorityCount()" /></li>
      }
    </ul>
  `,
})
export class ProductGrid {
  readonly products = input.required<readonly ProductCardModel[]>();
  /** How many leading cards load eagerly (the first row). */
  readonly priorityCount = input(4);
  protected readonly gridClass = productGridClass;
}
