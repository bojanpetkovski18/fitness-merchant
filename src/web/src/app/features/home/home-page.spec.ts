import { HttpRequest, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HomePage } from './home-page';

describe('HomePage', () => {
  it('says so when a product rail is empty instead of showing a bare heading', async () => {
    // M12 states audit: an empty rail rendered only its heading.
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(HomePage);
    const element = fixture.nativeElement as HTMLElement;
    TestBed.tick();
    const empty = { items: [], page: 1, pageSize: 4, totalCount: 0 };
    const sortOf = (r: HttpRequest<unknown>) =>
      new URL(r.urlWithParams, 'http://x').searchParams.get('sort');
    backend.match((r) => sortOf(r) === 'discount').forEach((r) => r.flush(empty));
    backend.match((r) => sortOf(r) === 'newest').forEach((r) => r.flush(empty));
    backend.match((r) => sortOf(r) === 'rating').forEach((r) => r.flush(empty));
    backend.match('/api/catalog/brands').forEach((r) => r.flush([]));
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();

    const deals = element.querySelector('section[aria-labelledby="deals-heading"]')!;
    expect(deals.textContent).toContain('Nothing here right now');
    expect(deals.querySelector('a[href="/supplements"]')).not.toBeNull();
    const brands = element.querySelector('section[aria-labelledby="brands-heading"]');
    expect(brands?.textContent).toContain('No brands yet');
  });
});
