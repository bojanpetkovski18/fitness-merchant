import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

/** Five stars filled to the average, with a text alternative ("4.5 out of 5, 12 reviews"). */
@Component({
  selector: 'app-rating-stars',
  imports: [DecimalPipe],
  template: `
    @if (count() > 0) {
      <p class="flex items-center gap-1.5 text-sm text-muted">
        <span aria-hidden="true" class="relative inline-block leading-none tracking-widest">
          <span class="text-border">★★★★★</span>
          <span class="absolute inset-0 overflow-hidden text-accent" [style.width.%]="fill()"
            >★★★★★</span
          >
        </span>
        <span class="sr-only">{{ average() | number: '1.1-1' }} out of 5,</span>
        <span
          >({{ count() }}<span class="sr-only"> {{ count() === 1 ? 'review' : 'reviews' }}</span
          >)</span
        >
      </p>
    } @else {
      <p class="text-sm text-muted">No reviews yet</p>
    }
  `,
})
export class RatingStars {
  readonly average = input.required<number>();
  readonly count = input.required<number>();

  protected readonly fill = computed(() => Math.max(0, Math.min(100, (this.average() / 5) * 100)));
}
