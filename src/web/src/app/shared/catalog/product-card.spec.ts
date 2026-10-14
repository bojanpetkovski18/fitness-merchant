import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProductCard as ProductCardModel } from '../../core/catalog/catalog.models';
import { ProductCard } from './product-card';

const base: ProductCardModel = {
  id: '1',
  slug: 'whey-isolate-vanilla-2-kg',
  name: 'Whey Isolate Vanilla 2 kg',
  brand: { slug: 'halvard-nutrition', name: 'Halvard Nutrition' },
  category: { slug: 'protein', name: 'Protein' },
  department: 'supplements',
  price: 59.9,
  discountPercent: 0,
  salePrice: 59.9,
  stockStatus: 'in-stock',
  ratingAverage: 0,
  ratingCount: 0,
  imageUrl: '/api/images/abc',
};

async function render(product: ProductCardModel): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    imports: [ProductCard],
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  const fixture = TestBed.createComponent(ProductCard);
  fixture.componentRef.setInput('product', product);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('ProductCard', () => {
  it('has exactly one link plus a named add-to-cart button', async () => {
    const element = await render(base);

    const links = element.querySelectorAll('a');
    expect(links.length).toBe(1);
    expect(links[0]!.textContent?.trim()).toBe('Whey Isolate Vanilla 2 kg');
    expect(links[0]!.getAttribute('href')).toBe('/products/whey-isolate-vanilla-2-kg');
    expect(element.querySelector('img')?.getAttribute('alt')).toBe('');
    expect(element.querySelector('button')?.getAttribute('aria-label')).toBe(
      'Add Whey Isolate Vanilla 2 kg to cart',
    );
  });

  it('shows the plain price, stock and "no reviews" without a discount', async () => {
    const element = await render(base);

    expect(element.textContent).toContain('€59.90');
    expect(element.querySelector('s')).toBeNull();
    expect(element.textContent).not.toContain('% off');
    expect(element.textContent).toContain('In stock');
    expect(element.textContent).toContain('No reviews yet');
  });

  it('shows the discount badge, sale price and struck original price', async () => {
    const element = await render({ ...base, discountPercent: 10, salePrice: 53.91 });

    expect(element.textContent).toContain('-10% off');
    expect(element.textContent).toContain('Sale price €53.91');
    expect(element.querySelector('s')?.textContent).toContain('Was €59.90');
  });

  it('marks out-of-stock products and dims their image', async () => {
    const element = await render({ ...base, stockStatus: 'out-of-stock' });

    expect(element.textContent).toContain('Out of stock');
    expect(element.querySelector('button')?.disabled).toBe(true);
    expect(element.querySelector('button')?.textContent?.trim()).toBe('Sold out');
    expect(element.querySelector('img')?.classList).toContain('opacity-60');
  });

  it('describes ratings in words for screen readers', async () => {
    const element = await render({ ...base, ratingAverage: 4.25, ratingCount: 12 });

    expect(element.textContent).toContain('4.3 out of 5');
    expect(element.textContent).toContain('12 reviews');
  });
});
