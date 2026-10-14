import { CurrencyPipe } from '@angular/common';
import { Component, input } from '@angular/core';

/** Sale price, plus the struck-through original when discounted. Amounts come from the API, never computed here (D012). */
@Component({
  selector: 'app-price-tag',
  imports: [CurrencyPipe],
  template: `
    <p class="flex flex-wrap items-baseline gap-x-2" [class.text-2xl]="large()">
      @if (discountPercent() > 0) {
        <span class="font-black text-accent">
          <span class="sr-only">Sale price </span>{{ salePrice() | currency: 'EUR' }}
        </span>
        <s class="text-sm text-muted">
          <span class="sr-only">Was </span>{{ price() | currency: 'EUR' }}
        </s>
      } @else {
        <span class="font-black text-fg">{{ price() | currency: 'EUR' }}</span>
      }
    </p>
  `,
})
export class PriceTag {
  readonly price = input.required<number>();
  readonly salePrice = input.required<number>();
  readonly discountPercent = input.required<number>();
  readonly large = input(false);
}
