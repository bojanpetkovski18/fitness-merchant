import { HttpRequest, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AdminProduct } from '../../core/admin/admin.models';
import { AdminProductFormPage } from './admin-product-form-page';
import { AdminProductsPage } from './admin-products-page';

@Component({ template: '' })
class Blank {}

const categories = [
  {
    id: 'c1',
    department: 'supplements',
    name: 'Protein',
    slug: 'protein',
    description: null,
    sortOrder: 1,
    productCount: 9,
  },
];
const brands = [
  {
    id: 'b1',
    name: 'Halvard',
    slug: 'halvard',
    description: 'x',
    accentColor: '#112233',
    logoImageId: null,
    logoUrl: null,
    productCount: 9,
  },
];
const product: AdminProduct = {
  id: 'p1',
  sku: 'HAL-0001',
  name: 'Whey Isolate Vanilla 2 kg',
  slug: 'whey-isolate-vanilla-2-kg',
  description: 'Invented.',
  categoryId: 'c1',
  brandId: 'b1',
  price: 59.9,
  discountPercent: 10,
  salePrice: 53.91,
  stockQuantity: 42,
  isActive: true,
  imageId: null,
  imageUrl: null,
  hasOrders: true,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
  version: 100,
};

/** Runs change detection and lets queued work run while requests may still be in flight. */
async function tick(): Promise<void> {
  TestBed.tick();
  await new Promise((resolve) => setTimeout(resolve));
}

/** After every request is flushed: wait until the app is stable. */
async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

describe('Admin pages', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(
          [
            { path: 'admin/products', component: AdminProductsPage },
            { path: '**', component: Blank },
          ],
          withComponentInputBinding(),
        ),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => (document.body.innerHTML = ''));

  /** Ticks until a matching request is issued (navigation takes a few turns), then returns it. */
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

  function flushLookups(): void {
    backend.match('/api/admin/categories').forEach((r) => r.flush(categories));
    backend.match('/api/admin/brands').forEach((r) => r.flush(brands));
  }

  async function renderForm(id?: string) {
    const fixture = TestBed.createComponent(AdminProductFormPage);
    if (id) fixture.componentRef.setInput('id', id);
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    await tick();
    flushLookups();
    if (id) backend.expectOne(`/api/admin/products/${id}`).flush(product);
    await settle(fixture);
    const field = (fieldId: string) => element.querySelector<HTMLInputElement>(`#${fieldId}`)!;
    return {
      fixture,
      element,
      field,
      type: (fieldId: string, value: string) => {
        field(fieldId).value = value;
        field(fieldId).dispatchEvent(new Event('input'));
        field(fieldId).dispatchEvent(new Event('change'));
      },
      submit: async () => {
        element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
        await tick(); // the request is still in flight, so not whenStable()
      },
    };
  }

  describe('product form', () => {
    it('shows linked client errors, focuses the first one and sends nothing', async () => {
      const { element, field, submit } = await renderForm();

      await submit();

      for (const id of ['p-name', 'p-sku', 'p-description', 'p-category', 'p-brand', 'p-price']) {
        expect(field(id).getAttribute('aria-invalid'), id).toBe('true');
        expect(field(id).getAttribute('aria-describedby'), id).toContain(`${id}-error`);
      }
      expect(element.querySelector('#p-name-error')?.textContent).toContain('Name is required.');
      expect(document.activeElement).toBe(field('p-name'));
      backend.expectNone((r) => r.method === 'POST');
    });

    it('maps server field errors, including the image, next to their fields', async () => {
      const { element, field, type, submit, fixture } = await renderForm();
      type('p-name', 'Lifting Straps');
      type('p-sku', 'hal-0001');
      type('p-description', 'Invented.');
      type('p-category', 'c1');
      type('p-brand', 'b1');
      type('p-price', '19.9');

      await submit();
      const request = backend.expectOne(
        (r) => r.method === 'POST' && r.url === '/api/admin/products',
      );
      expect(request.request.body).toEqual(
        expect.objectContaining({ sku: 'hal-0001', categoryId: 'c1', price: 19.9, version: null }),
      );
      request.flush(
        {
          status: 400,
          title: 'One or more validation errors occurred.',
          errors: {
            sku: ['Another product already uses this SKU.'],
            imageId: ['This image no longer exists. Upload it again.'],
          },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle(fixture);

      expect(element.querySelector('#p-sku-error')?.textContent).toContain(
        'Another product already uses this SKU.',
      );
      expect(document.activeElement).toBe(field('p-sku'));
      expect(element.querySelector('app-image-picker [role="alert"]')?.textContent).toContain(
        'This image no longer exists.',
      );
      expect(element.querySelector('[role="alert"]')?.textContent).toContain(
        'Please fix the highlighted fields.',
      );
    });

    it('sends the loaded version and, on 409, offers to reload the latest version', async () => {
      const { element, field, type, submit, fixture } = await renderForm('p1');
      expect(field('p-name').value).toBe('Whey Isolate Vanilla 2 kg');
      expect(field('p-price').value).toBe('59.9');

      type('p-price', '49.9');
      await submit();
      const put = backend.expectOne(
        (r) => r.method === 'PUT' && r.url === '/api/admin/products/p1',
      );
      expect(put.request.body).toEqual(expect.objectContaining({ price: 49.9, version: 100 }));
      put.flush(
        {
          status: 409,
          title:
            'Someone else changed this product since you opened it. Reload to see their changes.',
          code: 'concurrency',
        },
        { status: 409, statusText: 'Conflict' },
      );
      await settle(fixture);

      const alert = element.querySelector('[role="alert"]')!;
      expect(alert.textContent).toContain('Someone else changed this product');
      const reload = alert.querySelector('button')!;
      expect(document.activeElement).toBe(reload);

      reload.click();
      await tick();
      backend.expectOne('/api/admin/products/p1').flush({
        ...product,
        price: 64.9,
        name: 'Whey Isolate Vanilla 2 kg (new tub)',
        version: 101,
      });
      await settle(fixture);

      expect(element.querySelector('[role="alert"]')).toBeNull();
      expect(field('p-price').value).toBe('64.9');
      expect(field('p-name').value).toBe('Whey Isolate Vanilla 2 kg (new tub)');

      await submit();
      const retry = backend.expectOne((r) => r.method === 'PUT');
      expect(retry.request.body).toEqual(expect.objectContaining({ version: 101, price: 64.9 }));
      retry.flush({ ...product, price: 64.9, version: 102 });
      await settle(fixture);
      expect(element.querySelector('[role="status"]')?.textContent).toContain('Saved.');
    });

    it('refuses a file over 1 MB before uploading it', async () => {
      const { element, fixture } = await renderForm();
      const input = element.querySelector<HTMLInputElement>('app-image-picker input[type="file"]')!;
      const big = new File([new Uint8Array(1024 * 1024 + 1)], 'big.png', { type: 'image/png' });
      Object.defineProperty(input, 'files', { value: [big] });

      input.dispatchEvent(new Event('change'));
      await settle(fixture);

      expect(element.querySelector('app-image-picker [role="alert"]')?.textContent).toContain(
        'The image is larger than 1 MB.',
      );
      backend.expectNone('/api/admin/images');
    });
  });

  describe('products table', () => {
    it('marks the sorted column with aria-sort and toggles it through the URL', async () => {
      const harness = await RouterTestingHarness.create();
      const router = TestBed.inject(Router);
      const row = {
        id: 'p1',
        sku: 'HAL-0001',
        name: 'Whey',
        slug: 'whey',
        imageUrl: null,
        categoryName: 'Protein',
        brandName: 'Halvard',
        price: 59.9,
        discountPercent: 10,
        salePrice: 53.91,
        stockQuantity: 42,
        isActive: true,
        updatedAt: '2026-10-01T00:00:00Z',
      };

      // Navigation and the list request run together; flush the request, then the page is stable.
      void router.navigateByUrl('/admin/products?sort=name-asc&q=whey');
      const list = await nextRequest((r) => r.url === '/api/admin/products');
      flushLookups();
      expect([list.request.params.get('sort'), list.request.params.get('q')]).toEqual([
        'name-asc',
        'whey',
      ]);
      list.flush({ items: [row], page: 1, pageSize: 20, totalCount: 1 });
      await settle(harness.fixture);
      const element = harness.routeNativeElement!;

      const nameHeader = Array.from(element.querySelectorAll('th')).find((th) =>
        th.textContent?.includes('Name'),
      )!;
      expect(nameHeader.getAttribute('aria-sort')).toBe('ascending');
      expect(element.querySelectorAll('th[aria-sort]').length).toBe(1);
      expect(element.querySelector('caption')?.textContent).toContain('Name A–Z');

      const sortButton = nameHeader.querySelector('button')!;
      sortButton.focus();
      sortButton.click();
      const resorted = await nextRequest(
        (r) => r.url === '/api/admin/products' && r.params.get('sort') === 'name-desc',
      );
      await tick();
      expect(router.url).toContain('sort=name-desc');
      expect(router.url).toContain('q=whey'); // other filters survive a sort change
      // While the re-sorted page loads, the old rows stay and so does the focused header button
      // (keyboard users press Enter again to flip the direction).
      expect(element.querySelector('table')).not.toBeNull();
      expect(element.textContent).toContain('updating');
      expect(document.activeElement).toBe(sortButton);
      resorted.flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
      await settle(harness.fixture);
      expect(element.textContent).toContain('No products match');
    });
  });
});
