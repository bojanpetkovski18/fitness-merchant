import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Order, OrderItem } from '../../core/orders/order.models';
import { OrderSummaryView } from './order-pages';

const item = (overrides: Partial<OrderItem>): OrderItem => ({
  productId: crypto.randomUUID(),
  productSlug: 'whey-isolate-vanilla-2-kg',
  productName: 'Whey Isolate Vanilla 2 kg',
  sku: 'HAL-0001',
  unitPrice: 53.91,
  quantity: 1,
  lineTotal: 53.91,
  productAvailable: true,
  ...overrides,
});

const order: Order = {
  number: 'FM-000123',
  status: 'paid',
  placedAt: '2026-10-14T10:00:00Z',
  items: [
    item({}),
    item({
      productSlug: 'retired-shaker',
      productName: 'Retired Shaker',
      sku: 'X-1',
      unitPrice: 9.9,
      lineTotal: 9.9,
      productAvailable: false,
    }),
  ],
  subtotal: 63.81,
  shipping: 0,
  total: 63.81,
  currency: 'EUR',
  shippingAddress: {
    fullName: 'Mila Novak',
    line1: '10 Example Street',
    line2: null,
    city: 'Leipzig',
    postalCode: '04109',
    country: 'Germany',
    phone: '+49 000 000',
  },
  contactEmail: 'mila@test.example',
  payment: { provider: 'Fake', cardBrand: 'Visa', cardLast4: '4242' },
  history: [],
  canCancel: true,
  cancellationReason: null,
};

describe('OrderSummaryView', () => {
  it('links products still on sale and shows archived ones as plain text', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(OrderSummaryView);
    fixture.componentRef.setInput('order', order);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    const links = Array.from(element.querySelectorAll('a')).map((a) => [
      a.textContent?.trim(),
      a.getAttribute('href'),
    ]);
    expect(links).toEqual([['Whey Isolate Vanilla 2 kg', '/products/whey-isolate-vanilla-2-kg']]);
    expect(element.textContent).toContain('Retired Shaker (no longer sold)');
    expect(element.textContent).toContain('€9.90');
  });
});
