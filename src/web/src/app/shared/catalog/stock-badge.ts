import { Component, computed, input } from '@angular/core';
import { StockStatus } from '../../core/catalog/catalog.models';

@Component({
  selector: 'app-stock-badge',
  template: `
    <span class="inline-flex items-center gap-1.5 text-sm font-semibold" [class]="tone()">
      <span aria-hidden="true" class="size-2 rounded-full bg-current"></span>{{ label() }}
    </span>
  `,
})
export class StockBadge {
  readonly status = input.required<StockStatus>();

  protected readonly label = computed(
    () =>
      ({ 'in-stock': 'In stock', 'low-stock': 'Low stock', 'out-of-stock': 'Out of stock' })[
        this.status()
      ],
  );
  protected readonly tone = computed(
    () =>
      ({ 'in-stock': 'text-success', 'low-stock': 'text-accent', 'out-of-stock': 'text-danger' })[
        this.status()
      ],
  );
}
