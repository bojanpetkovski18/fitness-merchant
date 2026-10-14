export type CartLineIssue = 'unavailable' | 'out-of-stock' | 'exceeds-stock';

export interface CartLine {
  readonly productId: string;
  readonly slug: string;
  readonly name: string;
  readonly brandName: string;
  readonly imageUrl: string | null;
  readonly price: number;
  readonly discountPercent: number;
  readonly unitPrice: number;
  readonly quantity: number;
  readonly maxQuantity: number;
  readonly lineTotal: number;
  readonly issue: CartLineIssue | null;
}

/** Always priced by the server (also for guests, via the quote endpoint); the browser never computes money. */
export interface CartView {
  readonly lines: readonly CartLine[];
  readonly itemCount: number;
  readonly subtotal: number;
  readonly shipping: number;
  readonly total: number;
  readonly freeShippingThreshold: number;
  readonly amountToFreeShipping: number;
}

export interface CartItemInput {
  readonly productId: string;
  readonly quantity: number;
}

export const maxLineQuantity = 10;

export const emptyCart: CartView = {
  lines: [],
  itemCount: 0,
  subtotal: 0,
  shipping: 0,
  total: 0,
  freeShippingThreshold: 60,
  amountToFreeShipping: 60,
};
