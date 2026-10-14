import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from './toast.service';

/**
 * Two live regions that always exist in the DOM (regions added later are often not announced): polite for
 * confirmations, assertive for errors.
 */
@Component({
  selector: 'app-toast-host',
  imports: [RouterLink],
  template: `
    <div
      class="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-4 md:items-end"
    >
      <div
        role="status"
        aria-live="polite"
        class="flex w-full flex-col items-center gap-2 md:items-end"
      >
        @for (toast of info(); track toast.id) {
          <div
            class="pointer-events-auto flex w-full max-w-sm items-center gap-3 rounded-lg border border-border bg-surface px-4 py-3 text-sm shadow-lg"
          >
            <p class="flex-1 font-semibold">{{ toast.message }}</p>
            @if (toast.action; as action) {
              <a
                [routerLink]="action.link"
                class="font-bold text-accent underline underline-offset-4"
                (click)="toasts.dismiss(toast.id)"
                >{{ action.label }}</a
              >
            }
            <button
              type="button"
              class="px-1 text-muted hover:text-fg"
              (click)="toasts.dismiss(toast.id)"
            >
              <span aria-hidden="true">✕</span><span class="sr-only">Dismiss</span>
            </button>
          </div>
        }
      </div>
      <div
        role="alert"
        aria-live="assertive"
        class="flex w-full flex-col items-center gap-2 md:items-end"
      >
        @for (toast of errors(); track toast.id) {
          <div
            class="pointer-events-auto flex w-full max-w-sm items-center gap-3 rounded-lg border border-danger/50 bg-surface px-4 py-3 text-sm text-danger shadow-lg"
          >
            <p class="flex-1 font-semibold">{{ toast.message }}</p>
            <button type="button" class="px-1 hover:text-fg" (click)="toasts.dismiss(toast.id)">
              <span aria-hidden="true">✕</span><span class="sr-only">Dismiss</span>
            </button>
          </div>
        }
      </div>
    </div>
  `,
})
export class ToastHost {
  protected readonly toasts = inject(ToastService);
  protected readonly info = computed(() => this.toasts.toasts().filter((t) => t.tone === 'info'));
  protected readonly errors = computed(() =>
    this.toasts.toasts().filter((t) => t.tone === 'error'),
  );
}
