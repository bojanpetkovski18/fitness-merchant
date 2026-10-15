import { InteractivityChecker } from '@angular/cdk/a11y';
import { Dialog } from '@angular/cdk/dialog';
import { HttpRequest, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { AdminOrder, Dashboard } from '../../core/admin/admin.models';
import { Order } from '../../core/orders/order.models';
import { ToastService } from '../../core/ui/toast.service';
import { AdminDashboardPage } from './admin-dashboard-page';
import { AdminOrderPage } from './admin-order-pages';

const baseOrder: Order = {
  number: 'FM-000123',
  status: 'paid',
  placedAt: '2026-10-01T10:00:00Z',
  items: [],
  subtotal: 50,
  shipping: 4.9,
  total: 54.9,
  currency: 'EUR',
  shippingAddress: {
    fullName: 'Mila Novak',
    line1: '1 Street',
    line2: null,
    city: 'Leipzig',
    postalCode: '04109',
    country: 'Germany',
    phone: '+49 000',
  },
  contactEmail: 'mila@test.example',
  payment: { provider: 'Fake', cardBrand: 'Visa', cardLast4: '4242' },
  history: [],
  canCancel: true,
  cancellationReason: null,
};

function adminOrder(
  status: Order['status'],
  allowed: AdminOrder['allowedTransitions'],
): AdminOrder {
  return {
    order: { ...baseOrder, status },
    customer: { id: 'u1', email: 'mila@test.example', displayName: 'Mila Novak' },
    allowedTransitions: allowed,
    history: [
      {
        fromStatus: null,
        toStatus: 'pending-payment',
        at: '2026-10-01T10:00:00Z',
        note: null,
        changedBy: 'Mila Novak',
      },
    ],
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

describe('Admin orders and dashboard', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([], withComponentInputBinding()),
        // jsdom has no layout: let the CDK focus trap treat rendered controls as focusable.
        {
          provide: InteractivityChecker,
          useValue: {
            isDisabled: (el: Element) => (el as HTMLButtonElement).disabled === true,
            isVisible: () => true,
            isFocusable: (el: HTMLElement) => el.tabIndex >= 0,
            isTabbable: (el: HTMLElement) => el.tabIndex >= 0,
          },
        },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    TestBed.inject(Dialog).closeAll();
    document.body.innerHTML = '';
  });

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

  describe('order page', () => {
    async function render(order: AdminOrder) {
      const fixture = TestBed.createComponent(AdminOrderPage);
      fixture.componentRef.setInput('number', order.order.number);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      (await nextRequest((r) => r.url === `/api/admin/orders/${order.order.number}`)).flush(order);
      await settle(fixture);
      const actions = () =>
        Array.from(
          element.querySelectorAll<HTMLButtonElement>(
            'section[aria-labelledby="actions-heading"] button',
          ),
        ).map((b) => b.textContent?.trim());
      return { fixture, element, actions };
    }

    it('offers exactly the transitions the server allows', async () => {
      expect((await render(adminOrder('paid', ['shipped', 'cancelled']))).actions()).toEqual([
        'Mark as shipped',
        'Cancel order',
      ]);
      document.body.innerHTML = '';
      expect((await render(adminOrder('shipped', ['delivered']))).actions()).toEqual([
        'Mark as delivered',
      ]);
      document.body.innerHTML = '';
      const final = await render(adminOrder('delivered', []));
      expect(final.actions()).toEqual([]);
      expect(final.element.textContent).toContain('nothing more can change');
    });

    it('confirms in a dialog, sends the note and shows the new state', async () => {
      const { fixture, element, actions } = await render(
        adminOrder('paid', ['shipped', 'cancelled']),
      );
      element
        .querySelector<HTMLButtonElement>('section[aria-labelledby="actions-heading"] button')!
        .click();
      await settle(fixture);

      const dialog = document.querySelector<HTMLElement>('[role="dialog"]')!;
      expect(dialog.getAttribute('aria-modal')).toBe('true');
      expect(
        document.getElementById(dialog.getAttribute('aria-labelledby')!)?.textContent,
      ).toContain('Mark as shipped: FM-000123?');
      const note = dialog.querySelector<HTMLTextAreaElement>('#status-note')!;
      expect(document.activeElement).toBe(note);
      note.value = 'Tracking FM-TRACK-1';
      note.dispatchEvent(new Event('input'));
      dialog.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

      const post = await nextRequest((r) => r.method === 'POST');
      expect(post.request.url).toBe('/api/admin/orders/FM-000123/status');
      expect(post.request.body).toEqual({ toStatus: 'shipped', note: 'Tracking FM-TRACK-1' });
      post.flush(adminOrder('shipped', ['delivered']));
      await settle(fixture);

      expect(actions()).toEqual(['Mark as delivered']);
      expect(
        TestBed.inject(ToastService)
          .toasts()
          .map((t) => t.message),
      ).toContain('FM-000123 is now shipped.');
    });

    it('sends nothing when the dialog is dismissed', async () => {
      const { fixture, element } = await render(adminOrder('paid', ['shipped', 'cancelled']));
      element
        .querySelectorAll<HTMLButtonElement>(
          'section[aria-labelledby="actions-heading"] button',
        )[1]!
        .click();
      await settle(fixture);

      const dialog = document.querySelector<HTMLElement>('[role="alertdialog"]')!;
      const keep = Array.from(dialog.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Keep order',
      )!;
      keep.click();
      await settle(fixture);

      backend.expectNone((r) => r.method === 'POST');
    });
  });

  describe('dashboard', () => {
    const dashboard: Dashboard = {
      from: '2026-10-01',
      to: '2026-10-03',
      revenue: 194.7,
      orderCount: 3,
      averageOrderValue: 64.9,
      revenueByDay: [
        { day: '2026-10-01', revenue: 129.8, orders: 2 },
        { day: '2026-10-02', revenue: 0, orders: 0 },
        { day: '2026-10-03', revenue: 64.9, orders: 1 },
      ],
      topProducts: [
        { productId: 'p1', name: 'Whey Isolate', sku: 'HAL-0001', quantity: 2, revenue: 100 },
      ],
      lowStock: [{ productId: 'p2', name: 'Kettlebell 24 kg', sku: 'BRK-0001', stockQuantity: 0 }],
    };

    async function render() {
      const fixture = TestBed.createComponent(AdminDashboardPage);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      await tick();
      return { fixture, element };
    }

    it('shows a loading state, then tiles, an accessible chart and its table', async () => {
      const { fixture, element } = await render();
      expect(element.querySelector('[role="status"]')?.textContent).toContain(
        'Loading the dashboard',
      );

      const request = await nextRequest((r) => r.url === '/api/admin/dashboard');
      expect(request.request.params.keys().sort()).toEqual(['from', 'to']);
      request.flush(dashboard);
      await settle(fixture);

      expect(element.querySelector('dl')?.textContent).toContain('€194.70');
      const chart = element.querySelector('svg[role="img"]')!;
      expect(chart.getAttribute('aria-label')).toContain('Highest: €129.80 on 2026-10-01');
      expect(chart.querySelectorAll('rect').length).toBe(3);
      const rows = element.querySelectorAll('details tbody tr');
      expect(rows.length).toBe(3);
      expect(rows[1]!.textContent).toContain('€0.00');
      expect(element.textContent).toContain('Sold out');
      expect(element.querySelector('[aria-pressed="true"]')?.textContent).toContain('Last 30 days');
    });

    it('says so when nothing sold, and still lists low stock', async () => {
      const { fixture, element } = await render();
      (await nextRequest((r) => r.url === '/api/admin/dashboard')).flush({
        ...dashboard,
        revenue: 0,
        orderCount: 0,
        averageOrderValue: 0,
        revenueByDay: dashboard.revenueByDay.map((d) => ({ ...d, revenue: 0, orders: 0 })),
        topProducts: [],
      });
      await settle(fixture);

      expect(element.textContent).toContain('No sales in this range.');
      expect(element.querySelector('svg')).toBeNull();
      expect(element.textContent).toContain('No products sold in this range.');
      expect(element.textContent).toContain('Kettlebell 24 kg');
    });

    it('offers a retry when loading fails, and explains an invalid range', async () => {
      const { fixture, element } = await render();
      (await nextRequest((r) => r.url === '/api/admin/dashboard')).flush(null, {
        status: 500,
        statusText: 'Server Error',
      });
      await settle(fixture);
      expect(element.querySelector('[role="alert"]')?.textContent).toContain(
        "Couldn't load the dashboard",
      );

      element.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
      (await nextRequest((r) => r.url === '/api/admin/dashboard')).flush(
        {
          status: 400,
          title: 'Validation',
          errors: { to: ['The range can be at most 366 days.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle(fixture);
      expect(element.querySelector('[role="alert"]')?.textContent).toContain('at most 366 days');
    });
  });
});
