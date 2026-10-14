import { CartItemInput, maxLineQuantity } from './cart.models';

const storageKey = 'fm-cart';
const maxLines = 50;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * A guest's cart in localStorage: product IDs and quantities only, never names or prices (D015).
 * Anything malformed (edited by hand, older format) is dropped; storage errors (private mode) just mean the
 * cart is not remembered.
 */
export const guestCartStorage = {
  read(): CartItemInput[] {
    try {
      const parsed: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '[]');
      if (!Array.isArray(parsed)) return [];
      const items = new Map<string, number>();
      for (const entry of parsed.slice(0, maxLines)) {
        if (
          typeof entry === 'object' &&
          entry !== null &&
          typeof (entry as CartItemInput).productId === 'string' &&
          uuid.test((entry as CartItemInput).productId) &&
          Number.isInteger((entry as CartItemInput).quantity) &&
          (entry as CartItemInput).quantity >= 1
        ) {
          const { productId, quantity } = entry as CartItemInput;
          items.set(productId, Math.min((items.get(productId) ?? 0) + quantity, maxLineQuantity));
        }
      }
      return [...items].map(([productId, quantity]) => ({ productId, quantity }));
    } catch {
      return [];
    }
  },

  write(items: readonly CartItemInput[]): void {
    try {
      if (items.length === 0) {
        localStorage.removeItem(storageKey);
      } else {
        const minimal = items.map(({ productId, quantity }) => ({ productId, quantity }));
        localStorage.setItem(storageKey, JSON.stringify(minimal));
      }
    } catch {
      // Not remembered; the in-memory cart still works for this visit.
    }
  },

  clear(): void {
    this.write([]);
  },
};
