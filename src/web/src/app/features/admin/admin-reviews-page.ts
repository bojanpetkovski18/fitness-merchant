import { DatePipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, input, linkedSignal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Page } from '../../core/catalog/catalog.models';
import { toApiProblem } from '../../core/http/api-problem';
import { ToastService } from '../../core/ui/toast.service';
import { Pagination } from '../../shared/ui/pagination';
import { EmptyState, ErrorState } from '../../shared/ui/states';

interface AdminReview {
  readonly id: string;
  readonly productName: string;
  readonly productSlug: string;
  readonly rating: number;
  readonly title: string;
  readonly body: string;
  readonly author: string;
  readonly authorEmail: string;
  readonly isHidden: boolean;
  readonly createdAt: string;
}

/** Newest reviews first; hiding one takes it off the product page and out of the rating (SPEC F11). */
@Component({
  selector: 'app-admin-reviews-page',
  imports: [RouterLink, DatePipe, Pagination, EmptyState, ErrorState],
  template: `
    <h1 class="fm-display text-3xl md:text-4xl">Reviews</h1>
    <div class="mt-5 max-w-xs">
      <label for="review-status" class="block text-sm font-semibold">Show</label>
      <select
        id="review-status"
        #statusBox
        (change)="setStatus(statusBox.value)"
        class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2 text-fg"
      >
        <option value="" [selected]="!status()">All reviews</option>
        <option value="visible" [selected]="status() === 'visible'">Visible</option>
        <option value="hidden" [selected]="status() === 'hidden'">Hidden</option>
      </select>
    </div>

    <div class="mt-6">
      @if (reviews.error()) {
        <app-error-state title="Couldn't load reviews" (retry)="reviews.reload()" />
      } @else if (shown(); as page) {
        <p role="status" class="mb-3 text-sm text-muted">
          {{ page.totalCount }} {{ page.totalCount === 1 ? 'review' : 'reviews' }}
        </p>
        @if (page.totalCount === 0) {
          <app-empty-state title="No reviews here" message="Try showing all reviews." />
        } @else {
          <ul class="space-y-3">
            @for (r of page.items; track r.id) {
              <li class="rounded-xl border border-border bg-surface p-4">
                <div class="flex flex-wrap items-start justify-between gap-3">
                  <div class="min-w-0">
                    <p class="text-sm">
                      <a
                        [routerLink]="['/products', r.productSlug]"
                        class="font-semibold hover:text-accent"
                        >{{ r.productName }}</a
                      >
                      ·
                      <span
                        >{{ r.rating }}<span aria-hidden="true" class="text-accent">★</span
                        ><span class="sr-only"> out of 5 stars</span></span
                      >
                      @if (r.isHidden) {
                        ·
                        <span class="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-semibold"
                          >Hidden</span
                        >
                      }
                    </p>
                    <h2 class="mt-1 font-bold break-words">{{ r.title }}</h2>
                    <p class="text-xs break-all text-muted">
                      {{ r.author }} ({{ r.authorEmail }}) · {{ r.createdAt | date: 'medium' }}
                    </p>
                  </div>
                  <button
                    type="button"
                    class="shrink-0 rounded-md border px-3 py-1.5 text-sm font-bold aria-busy:opacity-60"
                    [class]="
                      r.isHidden
                        ? 'border-border hover:bg-surface-2'
                        : 'border-danger text-danger hover:bg-danger/10'
                    "
                    [attr.aria-busy]="busy() === r.id"
                    (click)="toggle(r)"
                  >
                    {{ r.isHidden ? 'Show again' : 'Hide'
                    }}<span class="sr-only"> the review “{{ r.title }}”</span>
                  </button>
                </div>
                <p class="mt-2 text-sm whitespace-pre-line break-words">{{ r.body }}</p>
              </li>
            }
          </ul>
          <app-pagination
            [page]="page.page"
            [pageSize]="page.pageSize"
            [totalCount]="page.totalCount"
          />
        }
      } @else {
        <p role="status" class="text-muted">Loading reviews…</p>
      }
    </div>
  `,
})
export class AdminReviewsPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly status = input<string>();
  readonly page = input<string>();

  protected readonly busy = signal<string | null>(null);
  protected readonly reviews = httpResource<Page<AdminReview>>(() => ({
    url: '/api/admin/reviews',
    params: {
      ...(this.status() ? { status: this.status()! } : {}),
      page: Number(this.page()) || 1,
    },
  }));
  protected readonly shown = linkedSignal<
    Page<AdminReview> | undefined,
    Page<AdminReview> | undefined
  >({
    source: () => (this.reviews.hasValue() ? this.reviews.value() : undefined),
    computation: (next, previous) => next ?? previous?.value,
  });

  protected setStatus(value: string): void {
    void this.router.navigate([], {
      queryParams: { status: value || null, page: null },
      queryParamsHandling: 'merge',
    });
  }

  /** The button stays enabled while saving: disabling it would drop keyboard focus to the page. */
  protected async toggle(review: AdminReview): Promise<void> {
    if (this.busy()) return;
    this.busy.set(review.id);
    try {
      const updated = await firstValueFrom(
        this.http.put<AdminReview>(`/api/admin/reviews/${review.id}/visibility`, {
          isHidden: !review.isHidden,
        }),
      );
      // Update in place so focus stays on the button; the filter may now exclude it until the next load.
      this.shown.update((page) =>
        page
          ? { ...page, items: page.items.map((r) => (r.id === updated.id ? updated : r)) }
          : page,
      );
      this.toasts.show(
        updated.isHidden
          ? 'The review is hidden and no longer counts.'
          : 'The review is visible again.',
      );
    } catch (error) {
      this.toasts.error(toApiProblem(error).title);
    } finally {
      this.busy.set(null);
    }
  }
}
