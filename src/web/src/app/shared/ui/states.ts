import { Component, input, output } from '@angular/core';

/** Placeholder cards while a product list loads; announced once to screen readers. */
@Component({
  selector: 'app-product-grid-skeleton',
  template: `
    <div role="status" aria-live="polite" class="sr-only">{{ label() }}</div>
    <ul aria-hidden="true" [class]="gridClass()">
      @for (i of placeholders(); track i) {
        <li class="animate-pulse overflow-hidden rounded-xl border border-border bg-surface">
          <div class="aspect-square bg-surface-2"></div>
          <div class="space-y-2 p-4">
            <div class="h-3 w-1/3 rounded bg-surface-2"></div>
            <div class="h-4 w-4/5 rounded bg-surface-2"></div>
            <div class="h-4 w-1/2 rounded bg-surface-2"></div>
          </div>
        </li>
      }
    </ul>
  `,
})
export class ProductGridSkeleton {
  readonly count = input(4);
  readonly label = input('Loading products…');
  readonly gridClass = input('grid grid-cols-2 gap-3 md:grid-cols-3 md:gap-4 lg:grid-cols-4');

  protected placeholders(): number[] {
    return Array.from({ length: this.count() }, (_, i) => i);
  }
}

/** A failed section: says what failed and offers a retry, without taking the rest of the page down. */
@Component({
  selector: 'app-error-state',
  template: `
    <div role="alert" class="rounded-xl border border-danger/40 bg-danger/5 px-4 py-6 text-center">
      <p class="font-bold text-fg">{{ title() }}</p>
      <p class="mt-1 text-sm text-muted">{{ message() }}</p>
      <button
        type="button"
        class="mt-4 rounded-md border border-border px-4 py-2 text-sm font-bold hover:bg-surface-2"
        (click)="retry.emit()"
      >
        Try again
      </button>
    </div>
  `,
})
export class ErrorState {
  readonly title = input("Couldn't load this section");
  readonly message = input('Check your connection and try again.');
  readonly retry = output();
}

/** Nothing to show: explains why and offers the way out through projected content. */
@Component({
  selector: 'app-empty-state',
  template: `
    <div class="rounded-xl border border-dashed border-border px-4 py-10 text-center">
      <p class="fm-display text-xl">{{ title() }}</p>
      <p class="mt-2 text-muted">{{ message() }}</p>
      <div class="mt-5 flex flex-wrap justify-center gap-3"><ng-content /></div>
    </div>
  `,
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly message = input('');
}
