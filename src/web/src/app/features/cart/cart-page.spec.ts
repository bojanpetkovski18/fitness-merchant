import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CartLine, emptyCart } from '../../core/cart/cart.models';
import { guestCartStorage } from '../../core/cart/guest-cart-storage';
import { CartStore } from '../../core/cart/cart.store';
import { CartPage } from './cart-page';

const id = '0199a000-0000-7000-8000-0000000000aa';
const base: CartLine = {
  productId: id,
  slug: 'kettlebell',
  name: 'Kettlebell 24 kg',
  brandName: 'Brakken Barbell Co.',
  imageUrl: null,
  price: 94.9,
  discountPercent: 15,
  unitPrice: 80.67,
  quantity: 3,
  maxQuantity: 1,
  lineTotal: 80.67,
  issue: 'exceeds-stock',
};

describe('CartPage', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [CartPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  it('shows the empty state for an empty cart', async () => {
    const fixture = TestBed.createComponent(CartPage);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Your cart is empty');
  });

  it('shows lines, a stock warning with a fix, the summary and blocks checkout', async () => {
    guestCartStorage.write([{ productId: id, quantity: 3 }]);
    const store = TestBed.inject(CartStore);
    const loading = store.load();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/cart/quote')
      .flush({
        ...emptyCart,
        lines: [base],
        itemCount: 1,
        subtotal: 80.67,
        total: 80.67,
        amountToFreeShipping: 0,
      });
    await loading;
    const fixture = TestBed.createComponent(CartPage);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    // The guest store already shrank the line to what is in stock and reported it in a toast.
    expect(
      element.querySelector('input[type="number"]')?.getAttribute('aria-label') ??
        element.querySelector('label')?.textContent,
    ).toContain('Quantity of Kettlebell 24 kg');
    expect(element.textContent).toContain('Order summary');
    expect(element.textContent).toContain('€80.67');
    expect(
      element.querySelector('button[aria-label="Remove Kettlebell 24 kg from cart"]'),
    ).not.toBeNull();
    expect(element.textContent).toContain('Sign in to check out');
  });

  it('keeps checkout disabled while a line is unavailable', async () => {
    guestCartStorage.write([{ productId: id, quantity: 1 }]);
    const store = TestBed.inject(CartStore);
    const loading = store.load();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/cart/quote')
      .flush({
        ...emptyCart,
        lines: [{ ...base, quantity: 1, maxQuantity: 0, lineTotal: 0, issue: 'unavailable' }],
      });
    await loading;
    const fixture = TestBed.createComponent(CartPage);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('No longer available. Remove it to continue.');
    const checkout = element.querySelector('a[href="/checkout"]');
    expect(checkout?.getAttribute('aria-disabled')).toBe('true');
    expect(checkout?.getAttribute('aria-describedby')).toBe('checkout-blocked');
  });
});
