import { Dialog } from '@angular/cdk/dialog';
import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartLine, maxLineQuantity } from '../../core/cart/cart.models';
import { CartStore } from '../../core/cart/cart.store';
import { SessionStore } from '../../core/auth/session.store';
import { PriceTag } from '../../shared/catalog/price-tag';
import { confirmAction } from '../../shared/ui/confirm-dialog';
import { EmptyState, ErrorState } from '../../shared/ui/states';
import { BusyButton } from '../../shared/ui/busy-button';

@Component({
  selector: 'app-cart-page',
  imports: [RouterLink, CurrencyPipe, PriceTag, EmptyState, ErrorState, BusyButton],
  templateUrl: './cart-page.html',
})
export class CartPage {
  protected readonly store = inject(CartStore);
  protected readonly session = inject(SessionStore);
  private readonly dialog = inject(Dialog);

  /** Steppers stop at what can be bought (stock, max 10). */
  protected maxFor(line: CartLine): number {
    return line.issue === null ? Math.min(line.maxQuantity, maxLineQuantity) : line.maxQuantity;
  }

  protected issueText(line: CartLine): string | null {
    switch (line.issue) {
      case 'unavailable':
        return 'No longer available. Remove it to continue.';
      case 'out-of-stock':
        return 'Out of stock. Remove it to continue.';
      case 'exceeds-stock':
        return `Only ${line.maxQuantity} left.`;
      default:
        return null;
    }
  }

  protected setQuantity(line: CartLine, value: number): void {
    const quantity = Math.max(1, Math.min(Math.trunc(value) || 1, this.maxFor(line) || 1));
    if (quantity !== line.quantity) {
      void this.store.setQuantity(line.productId, quantity);
    }
  }

  protected async clear(): Promise<void> {
    const confirmed = await confirmAction(this.dialog, {
      title: 'Empty your cart?',
      message: 'All items will be removed. This cannot be undone.',
      confirmLabel: 'Empty cart',
    });
    if (confirmed) {
      await this.store.clear();
    }
  }
}
