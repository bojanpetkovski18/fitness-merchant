import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../../core/auth/session.store';
import { Page } from '../../core/catalog/catalog.models';
import { ProductReviews, Review, reviewLimits } from '../../core/catalog/review.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { ToastService } from '../../core/ui/toast.service';
import { fieldError } from '../../shared/forms/field-error';
import { RatingStars } from '../../shared/catalog/rating-stars';
import { BusyButton } from '../../shared/ui/busy-button';
import { ErrorState } from '../../shared/ui/states';

const stars = [5, 4, 3, 2, 1] as const;

/**
 * Ratings and reviews of one product (SPEC F11): summary with a distribution, the reviews (newest first, more on
 * request) and the review form for verified buyers. Review text is shown through interpolation, so markup in it
 * is displayed as text, never run.
 */
@Component({
  selector: 'app-product-reviews',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    DecimalPipe,
    RatingStars,
    ErrorState,
    BusyButton,
  ],
  template: `
    <section id="reviews" aria-labelledby="reviews-heading" class="mt-14 scroll-mt-24">
      <h2 id="reviews-heading" class="fm-display mb-4 text-2xl">Reviews</h2>
      @if (data.error()) {
        <app-error-state title="Couldn't load the reviews" (retry)="data.reload()" />
      } @else if (data.hasValue()) {
        @let d = data.value();
        <div class="grid gap-8 lg:grid-cols-[18rem_1fr]">
          <div>
            @if (d.summary.count > 0) {
              <p class="text-4xl font-black">
                {{ d.summary.average | number: '1.1-1'
                }}<span class="text-base font-semibold text-muted"> / 5</span>
              </p>
              <app-rating-stars [average]="d.summary.average" [count]="d.summary.count" />
              <ul class="mt-4 space-y-1.5 text-sm" aria-label="Ratings by number of stars">
                @for (star of stars; track star; let i = $index) {
                  @let n = d.summary.distribution[i] ?? 0;
                  <li class="grid grid-cols-[3.5rem_1fr_2.5rem] items-center gap-2">
                    <span
                      >{{ star }} {{ star === 1 ? 'star' : 'stars'
                      }}<span class="sr-only">: </span></span
                    >
                    <span aria-hidden="true" class="h-2 overflow-hidden rounded-full bg-surface-2">
                      <span
                        class="block h-full rounded-full bg-accent"
                        [style.width.%]="percent(n, d.summary.count)"
                      ></span>
                    </span>
                    <span class="text-right text-muted"
                      >{{ n
                      }}<span class="sr-only">
                        {{ n === 1 ? 'review' : 'reviews' }},
                        {{ percent(n, d.summary.count) }}%</span
                      ></span
                    >
                  </li>
                }
              </ul>
            } @else {
              <p class="text-muted">No reviews yet.</p>
            }

            <div class="mt-6">
              @switch (d.viewer) {
                @case ('sign-in') {
                  <p class="text-sm">
                    <a
                      routerLink="/sign-in"
                      [queryParams]="{ returnUrl: returnUrl() }"
                      class="font-semibold text-accent underline underline-offset-4"
                      >Sign in</a
                    >
                    to review this product. Reviews come from customers who bought it.
                  </p>
                }
                @case ('not-purchased') {
                  <p class="text-sm text-muted">
                    Only customers who bought this product can review it.
                  </p>
                }
                @case ('already-reviewed') {
                  <p class="text-sm text-muted">Thanks, you have reviewed this product.</p>
                }
              }
            </div>
          </div>

          <div class="min-w-0">
            @if (d.viewer === 'can-review') {
              <form
                [formGroup]="form"
                (ngSubmit)="submit()"
                novalidate
                aria-labelledby="review-form-heading"
                class="mb-8 rounded-xl border border-border bg-surface p-5"
              >
                <h3 id="review-form-heading" class="font-bold">Write a review</h3>
                <div aria-live="assertive">
                  @if (problem(); as p) {
                    @if (p.status !== 400) {
                      <p role="alert" class="mt-3 text-sm font-semibold text-danger">
                        {{ p.title }}
                      </p>
                    }
                  }
                </div>

                @let ratingError = error('rating', 'Rating');
                <fieldset
                  class="mt-4"
                  [attr.aria-describedby]="ratingError ? 'review-rating-error' : null"
                >
                  <legend class="text-sm font-semibold">Your rating</legend>
                  <div class="mt-1 flex gap-1">
                    @for (star of ascending; track star) {
                      <span class="relative">
                        <input
                          type="radio"
                          class="peer absolute inset-0 opacity-0"
                          [id]="'review-star-' + star"
                          name="rating"
                          formControlName="rating"
                          [value]="star"
                          [attr.aria-invalid]="ratingError ? 'true' : null"
                        />
                        <label
                          [for]="'review-star-' + star"
                          class="block cursor-pointer rounded px-0.5 text-3xl leading-none peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-accent"
                          [class]="star <= (chosenRating() ?? 0) ? 'text-accent' : 'text-border'"
                          ><span aria-hidden="true">★</span
                          ><span class="sr-only"
                            >{{ star }} {{ star === 1 ? 'star' : 'stars' }}</span
                          ></label
                        >
                      </span>
                    }
                  </div>
                  @if (ratingError) {
                    <p id="review-rating-error" class="mt-1 text-sm text-danger">
                      {{ ratingError }}
                    </p>
                  }
                </fieldset>

                @let titleError = error('title', 'Title');
                <div class="mt-4">
                  <label for="review-title" class="block text-sm font-semibold">Title</label>
                  <input
                    id="review-title"
                    formControlName="title"
                    [attr.maxlength]="limits.titleMax"
                    [attr.aria-invalid]="titleError ? 'true' : null"
                    [attr.aria-describedby]="titleError ? 'review-title-error' : null"
                    class="mt-1 block w-full rounded-md border border-border bg-bg px-3 py-2 text-fg aria-invalid:border-danger"
                  />
                  @if (titleError) {
                    <p id="review-title-error" class="mt-1 text-sm text-danger">{{ titleError }}</p>
                  }
                </div>

                @let bodyError = error('body', 'Review');
                <div class="mt-4">
                  <label for="review-body" class="block text-sm font-semibold">Review</label>
                  <textarea
                    id="review-body"
                    formControlName="body"
                    rows="5"
                    [attr.maxlength]="limits.bodyMax"
                    [attr.aria-invalid]="bodyError ? 'true' : null"
                    [attr.aria-describedby]="
                      bodyError ? 'review-body-hint review-body-error' : 'review-body-hint'
                    "
                    class="mt-1 block w-full rounded-md border border-border bg-bg px-3 py-2 text-fg aria-invalid:border-danger"
                  ></textarea>
                  <p id="review-body-hint" class="mt-1 text-xs text-muted">
                    {{ limits.bodyMin }} to {{ limits.bodyMax }} characters. Your display name is
                    shown, never your email.
                  </p>
                  @if (bodyError) {
                    <p id="review-body-error" class="mt-1 text-sm text-danger">{{ bodyError }}</p>
                  }
                </div>

                <button
                  type="submit"
                  [appBusy]="pending()"
                  class="mt-4 rounded-md bg-accent px-5 py-2.5 font-bold text-on-accent hover:bg-accent-strong aria-disabled:opacity-60"
                >
                  {{ pending() ? 'Posting…' : 'Post review' }}
                </button>
              </form>
            }

            @if (reviews().length === 0) {
              @if (d.viewer === 'can-review') {
                <p class="text-muted">Be the first to review it.</p>
              }
            } @else {
              <ul class="space-y-5">
                @for (review of reviews(); track review.id) {
                  <li>
                    <article
                      [attr.aria-labelledby]="'review-' + review.id"
                      class="border-b border-border pb-5"
                    >
                      <p class="text-accent">
                        <span class="sr-only">Rated {{ review.rating }} out of 5</span>
                        <span aria-hidden="true">{{ '★★★★★'.slice(0, review.rating) }}</span
                        ><span aria-hidden="true" class="text-border">{{
                          '★★★★★'.slice(review.rating)
                        }}</span>
                      </p>
                      <h3 [id]="'review-' + review.id" class="mt-1 font-bold break-words">
                        {{ review.title }}
                      </h3>
                      <p class="text-xs text-muted">
                        {{ review.author }} ·
                        <time [attr.datetime]="review.createdAt">{{
                          review.createdAt | date: 'mediumDate'
                        }}</time>
                      </p>
                      <p class="mt-2 whitespace-pre-line break-words">{{ review.body }}</p>
                    </article>
                  </li>
                }
              </ul>
              @if (reviews().length < d.reviews.totalCount) {
                <div aria-live="polite" class="mt-5">
                  <button
                    type="button"
                    class="rounded-md border border-border px-4 py-2 font-bold hover:bg-surface-2 aria-disabled:opacity-60"
                    [appBusy]="loadingMore()"
                    (click)="loadMore()"
                  >
                    {{ loadingMore() ? 'Loading…' : 'Show more reviews' }}
                  </button>
                </div>
              }
            }
          </div>
        </div>
      } @else {
        <p role="status" class="text-muted">Loading reviews…</p>
      }
    </section>
  `,
})
export class ProductReviewsSection {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly slug = input.required<string>();
  /** A review was posted: the product's own rating changed too. */
  readonly reviewed = output();

  protected readonly stars = stars;
  protected readonly limits = reviewLimits;
  protected readonly ascending = [1, 2, 3, 4, 5] as const;

  protected readonly data = httpResource<ProductReviews>(() => {
    // Reading the user makes a sign-in or sign-out refetch, so "may I review?" stays right.
    void this.session.user();
    return `/api/catalog/products/${encodeURIComponent(this.slug())}/reviews`;
  });

  /** Page 1 from the resource, plus any pages loaded with "Show more"; resets when page 1 reloads. */
  protected readonly reviews = linkedSignal<readonly Review[]>(() =>
    this.data.hasValue() ? this.data.value().reviews.items : [],
  );
  private page = 1;
  protected readonly loadingMore = signal(false);

  protected readonly form = inject(FormBuilder).group({
    rating: [null as number | null, [Validators.required, Validators.min(1), Validators.max(5)]],
    title: [
      '',
      [
        Validators.required,
        Validators.minLength(reviewLimits.titleMin),
        Validators.maxLength(reviewLimits.titleMax),
      ],
    ],
    body: [
      '',
      [
        Validators.required,
        Validators.minLength(reviewLimits.bodyMin),
        Validators.maxLength(reviewLimits.bodyMax),
      ],
    ],
  });
  /** The chosen star count, to fill the stars up to it. */
  protected readonly chosenRating = toSignal(this.form.controls.rating.valueChanges, {
    initialValue: null,
  });
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);

  protected readonly returnUrl = computed(() => `${this.router.url.split('#')[0]}#reviews`);

  protected percent(n: number, total: number): number {
    return total > 0 ? Math.round((n / total) * 100) : 0;
  }

  protected error(field: 'rating' | 'title' | 'body', label: string): string | null {
    if (
      field === 'rating' &&
      this.submitted() &&
      !this.problem()?.fieldErrors['rating'] &&
      this.form.controls.rating.invalid
    ) {
      return 'Choose a rating from 1 to 5 stars.';
    }
    return fieldError(
      this.form.controls[field],
      label,
      this.problem()?.fieldErrors[field],
      this.submitted(),
    );
  }

  protected async loadMore(): Promise<void> {
    this.loadingMore.set(true);
    try {
      const next = await firstValueFrom(
        this.http.get<{ reviews: Page<Review> }>(
          `/api/catalog/products/${encodeURIComponent(this.slug())}/reviews`,
          { params: { page: this.page + 1 } },
        ),
      );
      this.page = next.reviews.page;
      this.reviews.update((list) => [...list, ...next.reviews.items]);
    } catch (error) {
      this.toasts.error(toApiProblem(error).title);
    } finally {
      this.loadingMore.set(false);
    }
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.problem.set(null);
    // Trimmed like the server does, so "   ok   " fails here with the same rule.
    const v = this.form.getRawValue();
    this.form.patchValue({ title: (v.title ?? '').trim(), body: (v.body ?? '').trim() });
    if (this.form.invalid) {
      this.focusFirstInvalid();
      return;
    }

    this.pending.set(true);
    try {
      const value = this.form.getRawValue();
      await firstValueFrom(
        this.http.post(`/api/catalog/products/${encodeURIComponent(this.slug())}/reviews`, {
          rating: value.rating,
          title: value.title,
          body: value.body,
        }),
      );
      this.toasts.show('Thanks! Your review is live.');
      this.form.reset();
      this.submitted.set(false);
      this.page = 1;
      this.data.reload();
      this.reviewed.emit();
    } catch (error) {
      this.problem.set(toApiProblem(error));
      this.focusFirstInvalid();
      if (this.problem()?.status === 409 || this.problem()?.status === 403) this.data.reload();
    } finally {
      this.pending.set(false);
    }
  }

  private focusFirstInvalid(): void {
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
      { injector: this.injector },
    );
  }
}
