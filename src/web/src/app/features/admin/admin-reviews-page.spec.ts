import { HttpRequest, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { AdminReviewsPage } from './admin-reviews-page';

const review = {
  id: 'r1',
  productName: 'Lever Lifting Belt',
  productSlug: 'lever-lifting-belt',
  rating: 4,
  title: 'Grippy',
  body: "<script>alert('x')</script>",
  author: 'Demo Customer',
  authorEmail: 'customer@fitness-merchant.example',
  isHidden: false,
  createdAt: '2026-10-15T10:00:00Z',
};

async function tick(): Promise<void> {
  TestBed.tick();
  await new Promise((resolve) => setTimeout(resolve));
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

describe('AdminReviewsPage', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([], withComponentInputBinding()),
      ],
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

  it('keeps the toggle focusable while saving, then flips its label in place', async () => {
    const fixture = TestBed.createComponent(AdminReviewsPage);
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    (await nextRequest((r) => r.url === '/api/admin/reviews')).flush({
      items: [review],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    await settle(fixture);
    expect(element.querySelector('li')?.textContent).toContain("<script>alert('x')</script>");

    const button = element.querySelector<HTMLButtonElement>('li button')!;
    button.focus();
    button.click();
    const put = await nextRequest((r) => r.method === 'PUT');
    await tick(); // render the saving state

    // A disabled button would push keyboard focus back to the page while the request runs.
    expect(button.disabled).toBe(false);
    expect(button.getAttribute('aria-busy')).toBe('true');
    expect(document.activeElement).toBe(button);
    button.click(); // a second press while saving sends nothing more
    backend.expectNone((r) => r.method === 'PUT');

    expect(put.request.body).toEqual({ isHidden: true });
    put.flush({ ...review, isHidden: true });
    await settle(fixture);

    expect(button.textContent).toContain('Show again');
    expect(document.activeElement).toBe(button);
    expect(element.textContent).toContain('Hidden');
  });
});
