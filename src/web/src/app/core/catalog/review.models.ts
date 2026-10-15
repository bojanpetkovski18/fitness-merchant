import { Page } from './catalog.models';

/** Whether the current visitor may review this product, and if not, why. */
export type ReviewEligibility = 'can-review' | 'sign-in' | 'not-purchased' | 'already-reviewed';

export interface Review {
  readonly id: string;
  readonly rating: number;
  readonly title: string;
  /** Plain text exactly as written; rendered through interpolation, so any markup shows as text. */
  readonly body: string;
  readonly author: string;
  readonly createdAt: string;
}

export interface ProductReviews {
  readonly summary: {
    readonly average: number;
    readonly count: number;
    /** Counts for 5, 4, 3, 2 and 1 stars, in that order. */
    readonly distribution: readonly number[];
  };
  readonly reviews: Page<Review>;
  readonly viewer: ReviewEligibility;
}

/** Client copies of the server's limits (Domain/Reviews/Review.cs); the server checks again. */
export const reviewLimits = { titleMin: 3, titleMax: 80, bodyMin: 10, bodyMax: 2000 } as const;
