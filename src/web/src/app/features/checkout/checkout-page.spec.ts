import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { CartView, emptyCart } from '../../core/cart/cart.models';
import { CartStore } from '../../core/cart/cart.store';
import { guestCartStorage } from '../../core/cart/guest-cart-storage';
import { CheckoutPage } from './checkout-page';

@Component({ template: '' })
class Blank {}

const productId = '0199a000-0000-7000-8000-0000000000aa';
const cart: CartView = {
  ...emptyCart,
  lines: [
    {
      productId,
      slug: 'whey',
      name: 'Whey Isolate',
      brandName: 'Halvard Nutrition',
      imageUrl: null,
      price: 59.9,
      discountPercent: 10,
      unitPrice: 53.91,
      quantity: 1,
      maxQuantity: 10,
      lineTotal: 53.91,
      issue: null,
    },
  ],
  itemCount: 1,
  subtotal: 53.91,
  shipping: 4.9,
  total: 58.81,
  amountToFreeShipping: 6.09,
};

describe('CheckoutPage', () => {
  let fixture: ComponentFixture<CheckoutPage>;
  let element: HTMLElement;
  let backend: HttpTestingController;

  const field = (id: string) =>
    element.querySelector<HTMLInputElement | HTMLSelectElement>(`#${id}`)!;
  const type = (id: string, value: string) => {
    const input = field(id);
    input.value = value;
    input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
  };
  const button = (text: string) =>
    [...element.querySelectorAll('button')].find((b) => b.textContent?.trim().startsWith(text))!;
  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  };

  beforeEach(async () => {
    localStorage.clear();
    guestCartStorage.write([{ productId, quantity: 1 }]);
    TestBed.configureTestingModule({
      imports: [CheckoutPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: Blank }]),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    // The store's own first load prices the guest cart; flush that request.
    TestBed.inject(CartStore);
    TestBed.tick();
    backend.expectOne('/api/cart/quote').flush(cart);
    await new Promise((resolve) => setTimeout(resolve));
    fixture = TestBed.createComponent(CheckoutPage);
    element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    await settle();
  });

  afterEach(() => element.remove());

  async function completeShipping(): Promise<void> {
    type('ship-fullName', 'Test Shopper');
    type('ship-line1', '10 Example Street');
    type('ship-city', 'Leipzig');
    type('ship-postalCode', '04109');
    type('ship-country', 'Germany');
    type('ship-phone', '+49 000 000 000');
    type('ship-email', 'shopper@test.example');
    button('Continue to payment').click();
    await settle();
  }

  async function completePayment(card = '4242 4242 4242 4242'): Promise<void> {
    type('pay-cardNumber', card);
    type('pay-expiry', '12/30');
    type('pay-cvc', '123');
    type('pay-nameOnCard', 'Test Shopper');
    button('Review order').click();
    await settle();
    backend.match('/api/cart/quote').forEach((r) => r.flush(cart)); // re-priced before review
    await settle();
  }

  it('keeps the shopper on step 1 with linked errors and focus on the first invalid field', async () => {
    type('ship-fullName', '');
    button('Continue to payment').click();
    await settle();

    expect(element.querySelector('#step-1-heading')).not.toBeNull();
    expect(field('ship-line1').getAttribute('aria-invalid')).toBe('true');
    expect(field('ship-line1').getAttribute('aria-describedby')).toBe('ship-line1-error');
    expect(element.querySelector('#ship-line1-error')?.textContent).toContain(
      'Address is required.',
    );
    expect(document.activeElement?.getAttribute('aria-invalid')).toBe('true');
  });

  it('moves through the steps with the current step marked and its heading focused', async () => {
    await completeShipping();

    expect(element.querySelector('[aria-current="step"]')?.textContent).toContain('2. Payment');
    expect(document.activeElement?.id).toBe('step-2-heading');
    expect(element.textContent).toContain('4242 4242 4242 4242'); // test-card hint

    button('Back').click();
    await settle();
    expect(field('ship-city').value).toBe('Leipzig'); // nothing lost going back
  });

  it('places the order once, with an idempotency key and the reviewed total, then shows the confirmation', async () => {
    await completeShipping();
    await completePayment();
    expect(element.querySelector('#step-3-heading')).not.toBeNull();
    expect(element.textContent).toContain('Card ending in 4242');

    const place = button('Place order');
    place.click();
    await settle();
    // Busy, not disabled (K12): it keeps keyboard focus, and a second press sends nothing.
    expect(place.getAttribute('aria-disabled')).toBe('true');
    expect(place.textContent).toContain('Placing order…');
    place.click();
    await settle();

    const request = backend.expectOne('/api/checkout');
    expect(request.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    expect(request.request.body.expectedTotal).toBe(58.81);
    expect(request.request.body.payment.cardNumber).toBe('4242 4242 4242 4242');
    request.flush({ number: 'FM-000041' }, { status: 201, statusText: 'Created' });
    await settle();
    backend.match('/api/cart/quote').forEach((r) => r.flush(emptyCart));
    await settle();

    expect(TestBed.inject(Router).url).toBe('/orders/FM-000041/confirmation');
  });

  it('returns to the payment step with the reason after a decline', async () => {
    await completeShipping();
    await completePayment('4000 0000 0000 0002');

    button('Place order').click();
    await settle();
    backend
      .expectOne('/api/checkout')
      .flush(
        { title: 'Your card was declined.', status: 402, code: 'payment-declined' },
        { status: 402, statusText: 'Payment Required' },
      );
    await settle();
    backend.match('/api/cart/quote').forEach((r) => r.flush(cart));
    await settle();

    expect(element.querySelector('#step-2-heading')).not.toBeNull();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Your card was declined. No money was taken and your cart has been kept.',
    );
    expect(field('pay-cvc').value).toBe('');
  });

  it('maps server field errors back to the shipping step', async () => {
    await completeShipping();
    await completePayment();

    button('Place order').click();
    await settle();
    backend.expectOne('/api/checkout').flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { 'shipping.postalCode': ['Enter a valid postal code.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();

    expect(element.querySelector('#step-1-heading')).not.toBeNull();
    expect(element.querySelector('#ship-postalCode-error')?.textContent).toContain(
      'Enter a valid postal code.',
    );
  });
});
