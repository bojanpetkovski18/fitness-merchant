import { CurrencyPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../../core/auth/session.store';
import { CartStore } from '../../core/cart/cart.store';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { Order, shippingCountries } from '../../core/orders/order.models';
import { fieldError } from '../../shared/forms/field-error';
import { EmptyState } from '../../shared/ui/states';

type Step = 1 | 2 | 3;
interface Alert {
  readonly message: string;
  readonly link?: { readonly label: string; readonly url: string };
}

type ShippingField =
  'fullName' | 'line1' | 'line2' | 'city' | 'postalCode' | 'country' | 'phone' | 'email';
type PaymentField = 'cardNumber' | 'expiry' | 'cvc' | 'nameOnCard';

/**
 * Three steps (shipping, payment, review) on one page. Steps are plain sections with a progress list
 * (aria-current="step") and focus moved to each step's heading, which works better with screen readers
 * than a carousel-style stepper. The server re-validates everything and has the final say on prices.
 */
@Component({
  selector: 'app-checkout-page',
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe, EmptyState],
  templateUrl: './checkout-page.html',
})
export class CheckoutPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  protected readonly cart = inject(CartStore);
  private readonly user = inject(SessionStore).user();

  protected readonly countries = shippingCountries;
  protected readonly step = signal<Step>(1);
  protected readonly submitting = signal(false);
  protected readonly alert = signal<Alert | null>(null);
  private readonly serverErrors = signal<ApiProblem['fieldErrors']>({});
  private readonly submitted = signal<Record<Step, boolean>>({ 1: false, 2: false, 3: false });
  /** One key per order attempt: a double click or network retry reuses it, a new attempt gets a new one. */
  private idempotencyKey = crypto.randomUUID();

  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly shipping = this.fb.group({
    fullName: [
      this.user?.displayName ?? '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(100)],
    ],
    line1: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    line2: ['', [Validators.maxLength(200)]],
    city: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    postalCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9 -]{3,20}$/)]],
    country: ['', [Validators.required]],
    phone: ['', [Validators.required, Validators.pattern(/^\+?[0-9 ()-]{6,30}$/)]],
    email: [
      this.user?.email ?? '',
      [Validators.required, Validators.email, Validators.maxLength(254)],
    ],
  });
  protected readonly payment = this.fb.group({
    cardNumber: ['', [Validators.required, Validators.pattern(/^[0-9 -]{12,23}$/)]],
    expiry: ['', [Validators.required, Validators.pattern(/^\d{1,2}\s*\/\s*\d{2}$/)]],
    cvc: ['', [Validators.required, Validators.pattern(/^\d{3,4}$/)]],
    nameOnCard: [
      this.user?.displayName ?? '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(100)],
    ],
  });

  protected readonly cardLast4 = computed(() => {
    this.step(); // re-read when moving to review
    return this.payment.controls.cardNumber.value.replace(/\D/g, '').slice(-4);
  });

  protected shippingError(field: ShippingField, label: string): string | null {
    return fieldError(
      this.shipping.controls[field],
      label,
      this.serverErrors()[`shipping.${field}`],
      this.submitted()[1],
    );
  }

  protected paymentError(field: PaymentField, label: string): string | null {
    const messages: Partial<Record<PaymentField, string>> = {
      cardNumber: 'Enter a valid card number.',
      expiry: 'Enter the expiry date as MM/YY.',
      cvc: 'Enter the 3 or 4 digit security code.',
    };
    const control = this.payment.controls[field];
    const server = this.serverErrors()[`payment.${field}`];
    if (!server && control.errors?.['pattern'] && (control.touched || this.submitted()[2])) {
      return messages[field] ?? null;
    }
    return fieldError(control, label, server, this.submitted()[2]);
  }

  protected continueFrom(step: 1 | 2): void {
    this.submitted.update((s) => ({ ...s, [step]: true }));
    const form = step === 1 ? this.shipping : this.payment;
    if (form.invalid) {
      this.focus('[aria-invalid="true"]');
      return;
    }
    this.goTo((step + 1) as Step);
    if (step === 2) {
      // Re-price right before review, so the customer confirms the current total.
      void this.cart.load();
    }
  }

  protected goTo(step: Step): void {
    this.step.set(step);
    this.focus(`#step-${step}-heading`);
  }

  protected async placeOrder(): Promise<void> {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.alert.set(null);
    this.serverErrors.set({});
    try {
      const order = await firstValueFrom(
        this.http.post<Order>(
          '/api/checkout',
          {
            shipping: this.shipping.getRawValue(),
            payment: this.payment.getRawValue(),
            expectedTotal: this.cart.cart().total,
          },
          { headers: new HttpHeaders({ 'Idempotency-Key': this.idempotencyKey }) },
        ),
      );
      await this.cart.load(); // the server emptied it
      await this.router.navigateByUrl(`/orders/${order.number}/confirmation`);
    } catch (error) {
      this.handleFailure(error);
    } finally {
      this.submitting.set(false);
    }
  }

  private handleFailure(error: unknown): void {
    const problem = toApiProblem(error);
    const code =
      error instanceof HttpErrorResponse
        ? (error.error as { code?: string } | null)?.code
        : undefined;
    this.idempotencyKey = crypto.randomUUID(); // the next attempt is a new order

    if (problem.status === 400 && Object.keys(problem.fieldErrors).length > 0) {
      this.serverErrors.set(problem.fieldErrors);
      const step = Object.keys(problem.fieldErrors).some((k) => k.startsWith('shipping.')) ? 1 : 2;
      this.alert.set({ message: 'Please check the highlighted fields.' });
      this.goTo(step);
      this.focus('[aria-invalid="true"]');
      return;
    }

    switch (code) {
      case 'payment-declined':
      case 'payment-error':
        this.payment.controls.cvc.reset();
        this.alert.set({
          message: `${problem.title} No money was taken and your cart has been kept.`,
        });
        void this.cart.load();
        this.goTo(2);
        return;
      case 'price-changed':
        this.alert.set({
          message:
            'Prices changed since you started checkout. Please check the new total and place your order again.',
        });
        void this.cart.load();
        this.focus('#checkout-alert');
        return;
      case 'cart-changed':
      case 'out-of-stock':
        this.alert.set({
          message: problem.title,
          link: { label: 'Review your cart', url: '/cart' },
        });
        void this.cart.load();
        this.focus('#checkout-alert');
        return;
      default:
        this.alert.set({ message: problem.title });
        this.focus('#checkout-alert');
    }
  }

  private focus(selector: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }
}
