import { Component, computed, input } from '@angular/core';
import { OrderStatus, orderStatusLabels } from '../../core/orders/order.models';

@Component({
  selector: 'app-order-status-badge',
  template: `<span
    class="inline-block rounded-full px-2.5 py-0.5 text-xs font-bold"
    [class]="tone()"
    >{{ label() }}</span
  >`,
})
export class OrderStatusBadge {
  readonly status = input.required<OrderStatus>();

  protected readonly label = computed(() => orderStatusLabels[this.status()]);
  protected readonly tone = computed(
    () =>
      ({
        'pending-payment': 'bg-surface-2 text-fg',
        paid: 'bg-accent text-on-accent',
        shipped: 'bg-surface-2 text-accent',
        delivered: 'bg-success text-bg',
        cancelled: 'bg-surface-2 text-danger',
      })[this.status()],
  );
}
