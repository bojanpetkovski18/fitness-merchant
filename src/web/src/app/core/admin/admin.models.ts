import { Order, OrderStatus } from '../orders/order.models';

/** Shapes of the /api/admin catalog endpoints (Features/Admin/AdminContracts.cs). */

export type Department = 'supplements' | 'gear';

export const departmentLabels: Record<Department, string> = {
  supplements: 'Supplements',
  gear: 'Gear',
};

export interface AdminProductRow {
  readonly id: string;
  readonly sku: string;
  readonly name: string;
  readonly slug: string;
  readonly imageUrl: string | null;
  readonly categoryName: string;
  readonly brandName: string;
  readonly price: number;
  readonly discountPercent: number;
  readonly salePrice: number;
  readonly stockQuantity: number;
  readonly isActive: boolean;
  readonly updatedAt: string;
}

export interface AdminProduct {
  readonly id: string;
  readonly sku: string;
  readonly name: string;
  readonly slug: string;
  readonly description: string;
  readonly categoryId: string;
  readonly brandId: string;
  readonly price: number;
  readonly discountPercent: number;
  readonly salePrice: number;
  readonly stockQuantity: number;
  readonly isActive: boolean;
  readonly imageId: string | null;
  readonly imageUrl: string | null;
  readonly hasOrders: boolean;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly version: number;
}

export interface ProductInput {
  readonly sku: string;
  readonly name: string;
  readonly slug: string;
  readonly description: string;
  readonly categoryId: string | null;
  readonly brandId: string | null;
  readonly price: number | null;
  readonly discountPercent: number | null;
  readonly stockQuantity: number | null;
  readonly isActive: boolean;
  readonly imageId: string | null;
  readonly version: number | null;
}

export interface AdminCategory {
  readonly id: string;
  readonly department: Department;
  readonly name: string;
  readonly slug: string;
  readonly description: string | null;
  readonly sortOrder: number;
  readonly productCount: number;
}

export interface AdminBrand {
  readonly id: string;
  readonly name: string;
  readonly slug: string;
  readonly description: string;
  readonly accentColor: string;
  readonly logoImageId: string | null;
  readonly logoUrl: string | null;
  readonly productCount: number;
}

export interface AdminImage {
  readonly id: string;
  readonly url: string;
  readonly contentType: string;
  readonly width: number;
  readonly height: number;
  readonly sizeBytes: number;
  readonly createdAt: string;
}

/** The image a form points at: enough to preview it and to send its ID. */
export interface PickedImage {
  readonly id: string;
  readonly url: string;
}

export type DeleteOutcome = 'deleted' | 'archived';

/** Allow-listed sort keys, mirrored from AdminProductCriteria.SortKeys. */
export type AdminProductSortKey = 'updated' | 'name' | 'sku' | 'price' | 'stock';
export type SortDirection = 'asc' | 'desc';

/** Client-side copy of the upload limit, for an early message; the server checks again. */
export const maxUploadBytes = 1024 * 1024;
export const acceptedImageTypes = 'image/png,image/jpeg,image/webp';

export interface AdminOrderRow {
  readonly number: string;
  readonly status: OrderStatus;
  readonly placedAt: string;
  readonly customerName: string;
  readonly contactEmail: string;
  readonly itemCount: number;
  readonly total: number;
}

export interface AdminStatusChange {
  readonly fromStatus: OrderStatus | null;
  readonly toStatus: OrderStatus;
  readonly at: string;
  readonly note: string | null;
  readonly changedBy: string | null;
}

export interface AdminOrder {
  readonly order: Order;
  readonly customer: { readonly id: string; readonly email: string; readonly displayName: string };
  readonly allowedTransitions: readonly OrderStatus[];
  readonly history: readonly AdminStatusChange[];
}

export interface Dashboard {
  readonly from: string;
  readonly to: string;
  readonly revenue: number;
  readonly orderCount: number;
  readonly averageOrderValue: number;
  readonly revenueByDay: readonly {
    readonly day: string;
    readonly revenue: number;
    readonly orders: number;
  }[];
  readonly topProducts: readonly {
    readonly productId: string;
    readonly name: string;
    readonly sku: string;
    readonly quantity: number;
    readonly revenue: number;
  }[];
  readonly lowStock: readonly {
    readonly productId: string;
    readonly name: string;
    readonly sku: string;
    readonly stockQuantity: number;
  }[];
}

/** What an admin action button says for each target status. */
export const transitionLabels: Record<OrderStatus, string> = {
  'pending-payment': 'Mark as awaiting payment',
  paid: 'Mark as paid',
  shipped: 'Mark as shipped',
  delivered: 'Mark as delivered',
  cancelled: 'Cancel order',
};

/** A UTC calendar day as the API expects it (yyyy-MM-dd). */
export function isoDay(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** The last `days` days ending today (UTC), inclusive. */
export function lastDays(days: number, today = new Date()): { from: string; to: string } {
  const from = new Date(today);
  from.setUTCDate(from.getUTCDate() - (days - 1));
  return { from: isoDay(from), to: isoDay(today) };
}
