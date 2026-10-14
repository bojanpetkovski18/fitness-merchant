export type OrderStatus = 'pending-payment' | 'paid' | 'shipped' | 'delivered' | 'cancelled';

export const orderStatusLabels: Record<OrderStatus, string> = {
  'pending-payment': 'Awaiting payment',
  paid: 'Paid',
  shipped: 'Shipped',
  delivered: 'Delivered',
  cancelled: 'Cancelled',
};

export interface ShippingAddress {
  readonly fullName: string;
  readonly line1: string;
  readonly line2: string | null;
  readonly city: string;
  readonly postalCode: string;
  readonly country: string;
  readonly phone: string;
}

export interface OrderItem {
  readonly productId: string;
  readonly productSlug: string;
  readonly productName: string;
  readonly sku: string;
  readonly unitPrice: number;
  readonly quantity: number;
  readonly lineTotal: number;
  /** False once the product is archived: show the name as text, there is no page to link to. */
  readonly productAvailable: boolean;
}

export interface Order {
  readonly number: string;
  readonly status: OrderStatus;
  readonly placedAt: string;
  readonly items: readonly OrderItem[];
  readonly subtotal: number;
  readonly shipping: number;
  readonly total: number;
  readonly currency: string;
  readonly shippingAddress: ShippingAddress;
  readonly contactEmail: string;
  readonly payment: {
    readonly provider: string;
    readonly cardBrand: string | null;
    readonly cardLast4: string | null;
  };
  readonly history: readonly {
    readonly status: OrderStatus;
    readonly at: string;
    readonly note: string | null;
  }[];
  readonly canCancel: boolean;
  readonly cancellationReason: string | null;
}

export interface OrderSummary {
  readonly number: string;
  readonly status: OrderStatus;
  readonly placedAt: string;
  readonly total: number;
  readonly itemCount: number;
}

/** Same list the server accepts (CheckoutValidation.Countries); the server stays authoritative. */
export const shippingCountries = [
  'Austria',
  'Belgium',
  'Croatia',
  'Denmark',
  'Finland',
  'France',
  'Germany',
  'Greece',
  'Ireland',
  'Italy',
  'Netherlands',
  'North Macedonia',
  'Poland',
  'Portugal',
  'Slovenia',
  'Spain',
  'Sweden',
] as const;
