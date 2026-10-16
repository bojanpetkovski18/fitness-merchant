import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Page, ProductCard } from '../../core/catalog/catalog.models';
import { ListingPage } from './listing-page';

const product: ProductCard = {
  id: '1',
  slug: 'whey',
  name: 'Whey Isolate',
  brand: { slug: 'halvard-nutrition', name: 'Halvard Nutrition' },
  category: { slug: 'protein', name: 'Protein' },
  department: 'supplements',
  price: 10,
  discountPercent: 0,
  salePrice: 10,
  stockStatus: 'in-stock',
  ratingAverage: 0,
  ratingCount: 0,
  imageUrl: null,
};

const page = (items: ProductCard[], totalCount = items.length): Page<ProductCard> => ({
  items,
  page: 1,
  pageSize: 24,
  totalCount,
});

describe('ListingPage', () => {
  let backend: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(
          [{ path: 'supplements', component: ListingPage, data: { department: 'supplements' } }],
          withComponentInputBinding(),
        ),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  function flushReferenceData(): void {
    backend.expectOne('/api/catalog/departments').flush([
      {
        slug: 'supplements',
        name: 'Supplements',
        categories: [{ slug: 'protein', name: 'Protein', description: null }],
      },
    ]);
    // K8: only brands with products in this department.
    backend.expectOne('/api/catalog/brands?department=supplements').flush([]);
  }

  /** Lets a click-triggered navigation finish, then renders; the new products request is left for the test. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  function productsRequest(): TestRequest {
    return backend.expectOne((r) => r.url.startsWith('/api/catalog/products'));
  }

  it('reads filters from the URL into the API request', async () => {
    await harness.navigateByUrl('/supplements?category=protein&minPrice=10&sort=price-asc&page=2');
    TestBed.tick();

    const request = productsRequest();
    expect(request.request.url).toBe(
      '/api/catalog/products?department=supplements&category=protein&minPrice=10&sort=price-asc&page=2&pageSize=24',
    );
    request.flush(page([product], 30));
    flushReferenceData();
    await harness.fixture.whenStable();

    const checkbox =
      harness.routeNativeElement!.querySelector<HTMLInputElement>('input[type="checkbox"]');
    expect(checkbox?.checked).toBe(true);
    expect(harness.routeNativeElement!.querySelector('[role="status"]')?.textContent).toContain(
      'of 30 products',
    );
  });

  it('writes a filter change to the URL and resets to page 1', async () => {
    await harness.navigateByUrl('/supplements?page=3');
    TestBed.tick();
    productsRequest().flush(page([product], 80));
    flushReferenceData();
    await harness.fixture.whenStable();

    harness.routeNativeElement!.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click();
    await settle();

    expect(TestBed.inject(Router).url).toBe('/supplements?category=protein');
    expect(productsRequest().request.url).toContain('category=protein&page=1');
  });

  it('shows an empty state with "Clear filters" when nothing matches', async () => {
    await harness.navigateByUrl('/supplements?q=nothing');
    TestBed.tick();
    productsRequest().flush(page([]));
    flushReferenceData();
    await harness.fixture.whenStable();

    const element = harness.routeNativeElement!;
    expect(element.textContent).toContain('No products found');
    const clear = [...element.querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Clear filters'),
    );
    expect(clear).toBeDefined();

    clear!.click();
    await settle();
    expect(TestBed.inject(Router).url).toBe('/supplements');
    productsRequest().flush(page([product]));
  });

  it('shows an error state with retry when the API fails', async () => {
    await harness.navigateByUrl('/supplements');
    TestBed.tick();
    productsRequest().flush(null, { status: 500, statusText: 'Server Error' });
    flushReferenceData();
    await harness.fixture.whenStable();

    const alert = harness.routeNativeElement!.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain("Couldn't load products");
    alert!.querySelector('button')!.click();
    TestBed.tick();
    productsRequest().flush(page([product]));
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Whey Isolate');
  });
});
