export type Department = 'supplements' | 'gear';
export type StockStatus = 'in-stock' | 'low-stock' | 'out-of-stock';
export type ProductSort =
  'relevance' | 'newest' | 'price-asc' | 'price-desc' | 'rating' | 'discount';

export interface NamedRef {
  readonly slug: string;
  readonly name: string;
}

export interface Page<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface CategoryInfo {
  readonly slug: string;
  readonly name: string;
  readonly description: string | null;
}

export interface DepartmentInfo {
  readonly slug: Department;
  readonly name: string;
  readonly categories: readonly CategoryInfo[];
}

export interface BrandInfo {
  readonly slug: string;
  readonly name: string;
  readonly description: string;
  readonly accentColor: string;
  readonly logoUrl: string | null;
  readonly productCount: number;
}

export interface ProductCard {
  readonly id: string;
  readonly slug: string;
  readonly name: string;
  readonly brand: NamedRef;
  readonly category: NamedRef;
  readonly department: Department;
  readonly price: number;
  readonly discountPercent: number;
  readonly salePrice: number;
  readonly stockStatus: StockStatus;
  readonly ratingAverage: number;
  readonly ratingCount: number;
  readonly imageUrl: string | null;
}

export interface ProductDetail extends ProductCard {
  readonly sku: string;
  readonly description: string;
  readonly maxOrderQuantity: number;
}
