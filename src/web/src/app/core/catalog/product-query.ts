import { Params } from '@angular/router';
import { Department, ProductSort } from './catalog.models';

/**
 * Listing state, kept entirely in the URL query string (SPEC F2) so reload, back/forward and shared links
 * reproduce the same view. Unknown or malformed values fall back to defaults instead of breaking the page;
 * the API still validates everything it receives.
 */
export interface ProductQuery {
  readonly q: string;
  readonly categories: readonly string[];
  readonly brands: readonly string[];
  readonly minPrice: number | null;
  readonly maxPrice: number | null;
  readonly inStock: boolean;
  readonly sort: ProductSort | null;
  readonly page: number;
}

export const emptyProductQuery: ProductQuery = {
  q: '',
  categories: [],
  brands: [],
  minPrice: null,
  maxPrice: null,
  inStock: false,
  sort: null,
  page: 1,
};

export const sortOptions: readonly { value: ProductSort; label: string }[] = [
  { value: 'relevance', label: 'Best match' },
  { value: 'newest', label: 'Newest' },
  { value: 'price-asc', label: 'Price: low to high' },
  { value: 'price-desc', label: 'Price: high to low' },
  { value: 'discount', label: 'Biggest discount' },
  { value: 'rating', label: 'Top rated' },
];

export const pageSize = 24;

export function parseProductQuery(params: Params): ProductQuery {
  const sort = single(params['sort']);
  return {
    q: (single(params['q']) ?? '').trim().slice(0, 100),
    categories: list(params['category']),
    brands: list(params['brand']),
    minPrice: price(single(params['minPrice'])),
    maxPrice: price(single(params['maxPrice'])),
    inStock: single(params['inStock']) === 'true',
    sort: sortOptions.some((o) => o.value === sort) ? (sort as ProductSort) : null,
    page: positiveInt(single(params['page'])) ?? 1,
  };
}

/** Router query params for a state; defaults are omitted so URLs stay short. */
export function toQueryParams(query: ProductQuery): Params {
  return {
    q: query.q || null,
    category: query.categories.length ? [...query.categories] : null,
    brand: query.brands.length ? [...query.brands] : null,
    minPrice: query.minPrice ?? null,
    maxPrice: query.maxPrice ?? null,
    inStock: query.inStock ? 'true' : null,
    sort: query.sort,
    page: query.page > 1 ? query.page : null,
  };
}

/** The API URL for a listing state within a department (or across the catalog when department is null). */
export function productsApiUrl(
  query: ProductQuery,
  department: Department | null,
  size = pageSize,
): string {
  const params = new URLSearchParams();
  if (department) params.set('department', department);
  if (query.q) params.set('q', query.q);
  query.categories.forEach((c) => params.append('category', c));
  query.brands.forEach((b) => params.append('brand', b));
  if (query.minPrice !== null) params.set('minPrice', String(query.minPrice));
  if (query.maxPrice !== null) params.set('maxPrice', String(query.maxPrice));
  if (query.inStock) params.set('inStock', 'true');
  if (query.sort) params.set('sort', query.sort);
  params.set('page', String(query.page));
  params.set('pageSize', String(size));
  return `/api/catalog/products?${params.toString()}`;
}

export function hasActiveFilters(query: ProductQuery): boolean {
  return (
    query.q !== '' ||
    query.categories.length > 0 ||
    query.brands.length > 0 ||
    query.minPrice !== null ||
    query.maxPrice !== null ||
    query.inStock
  );
}

function single(value: unknown): string | undefined {
  if (Array.isArray(value)) return typeof value[0] === 'string' ? value[0] : undefined;
  return typeof value === 'string' ? value : undefined;
}

function list(value: unknown): string[] {
  const values = Array.isArray(value) ? value : value === undefined ? [] : [value];
  return [
    ...new Set(values.filter((v): v is string => typeof v === 'string' && /^[a-z0-9-]+$/.test(v))),
  ];
}

function price(value: string | undefined): number | null {
  if (value === undefined || value === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

function positiveInt(value: string | undefined): number | null {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 1 ? parsed : null;
}
