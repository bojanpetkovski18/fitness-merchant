import { HttpRequest, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProductReviews, ReviewEligibility } from '../../core/catalog/review.models';
import { ProductReviewsSection } from './product-reviews';

const url = '/api/catalog/products/lifting-straps/reviews';

function reviews(
  viewer: ReviewEligibility,
  body = 'Held up well through heavy pulls.',
): ProductReviews {
  return {
    summary: { average: 4.5, count: 2, distribution: [1, 1, 0, 0, 0] },
    reviews: {
      items: [
        {
          id: 'r1',
          rating: 5,
          title: 'Great straps',
          body,
          author: 'Mila N.',
          createdAt: '2026-10-01T10:00:00Z',
        },
        {
          id: 'r2',
          rating: 4,
          title: 'Solid',
          body: 'Good grip, thick stitching.',
          author: 'Luka',
          createdAt: '2026-09-28T10:00:00Z',
        },
      ],
      page: 1,
      pageSize: 5,
      totalCount: 2,
    },
    viewer,
  };
}

async function tick(): Promise<void> {
  TestBed.tick();
  await new Promise((resolve) => setTimeout(resolve));
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

describe('ProductReviewsSection', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => (document.body.innerHTML = ''));

  async function nextRequest(
    predicate: (r: HttpRequest<unknown>) => boolean,
  ): Promise<TestRequest> {
    for (let i = 0; i < 20; i++) {
      const [found] = backend.match(predicate);
      if (found) return found;
      await tick();
    }
    throw new Error('The expected request was never sent.');
  }

  async function render(data: ProductReviews) {
    const fixture = TestBed.createComponent(ProductReviewsSection);
    fixture.componentRef.setInput('slug', 'lifting-straps');
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    (await nextRequest((r) => r.url === url && r.method === 'GET')).flush(data);
    await settle(fixture);
    return { fixture, element };
  }

  it('asks guests to sign in and comes back to the reviews afterwards', async () => {
    const { element } = await render(reviews('sign-in'));

    const link = element.querySelector<HTMLAnchorElement>('a[href^="/sign-in"]')!;
    expect(link.textContent).toContain('Sign in');
    expect(decodeURIComponent(link.getAttribute('href')!)).toContain('returnUrl=/#reviews');
    expect(element.querySelector('form')).toBeNull();
  });

  it('explains why there is no form for people who did not buy it or already reviewed it', async () => {
    const notBought = await render(reviews('not-purchased'));
    expect(notBought.element.textContent).toContain(
      'Only customers who bought this product can review it.',
    );
    expect(notBought.element.querySelector('form')).toBeNull();
    document.body.innerHTML = '';

    const done = await render(reviews('already-reviewed'));
    expect(done.element.textContent).toContain('you have reviewed this product');
    expect(done.element.querySelector('form')).toBeNull();
  });

  it('gives the distribution as text, not only as bars', async () => {
    const { element } = await render(reviews('sign-in'));

    const rows = Array.from(
      element.querySelectorAll('ul[aria-label="Ratings by number of stars"] li'),
    ).map((li) => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(rows[0]).toBe('5 stars: 1 review, 50%');
    expect(rows[4]).toBe('1 star: 0 reviews, 0%');
  });

  it('shows markup in a review as text', async () => {
    const { element } = await render(reviews('sign-in', "<script>alert('x')</script><b>bold?</b>"));

    const body = element.querySelectorAll('article p')[2]!;
    expect(body.textContent).toContain("<script>alert('x')</script><b>bold?</b>");
    expect(element.querySelector('article script, article b')).toBeNull();
  });

  it('validates the form, then posts the trimmed review and refreshes', async () => {
    const { fixture, element } = await render(reviews('can-review'));
    let reviewed = 0;
    fixture.componentInstance.reviewed.subscribe(() => reviewed++);
    const submit = () =>
      element.querySelector<HTMLButtonElement>('form button[type="submit"]')!.click();
    const type = (id: string, value: string) => {
      const field = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(`#${id}`)!;
      field.value = value;
      field.dispatchEvent(new Event('input'));
    };

    type('review-title', '  ok  ');
    submit();
    await settle(fixture);

    expect(element.querySelector('#review-rating-error')?.textContent).toContain(
      'Choose a rating from 1 to 5 stars.',
    );
    expect(element.querySelector('#review-title-error')?.textContent).toContain(
      'at least 3 characters',
    );
    expect(element.querySelector('#review-body-error')?.textContent).toContain(
      'Review is required.',
    );
    expect(document.activeElement?.id).toBe('review-star-1');
    backend.expectNone((r) => r.method === 'POST');

    element.querySelector<HTMLInputElement>('#review-star-4')!.click();
    type('review-title', '  Strong straps ');
    type('review-body', ' Held up through a month of heavy deadlifts. ');
    submit();
    const post = await nextRequest((r) => r.method === 'POST');
    expect(post.request.url).toBe(url);
    expect(post.request.body).toEqual({
      rating: 4,
      title: 'Strong straps',
      body: 'Held up through a month of heavy deadlifts.',
    });
    post.flush({}, { status: 201, statusText: 'Created' });

    (await nextRequest((r) => r.url === url && r.method === 'GET')).flush(
      reviews('already-reviewed'),
    );
    await settle(fixture);
    expect(reviewed).toBe(1);
    expect(element.querySelector('form')).toBeNull();
    expect(element.textContent).toContain('you have reviewed this product');
  });
});
