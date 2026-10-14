import {
  emptyProductQuery,
  hasActiveFilters,
  parseProductQuery,
  productsApiUrl,
  toQueryParams,
} from './product-query';

describe('product query URL state', () => {
  it('parses a full query string', () => {
    const query = parseProductQuery({
      q: '  whey ',
      category: ['protein', 'protein', 'pre-workout'],
      brand: 'halvard-nutrition',
      minPrice: '10',
      maxPrice: '59.9',
      inStock: 'true',
      sort: 'price-asc',
      page: '3',
    });

    expect(query).toEqual({
      q: 'whey',
      categories: ['protein', 'pre-workout'],
      brands: ['halvard-nutrition'],
      minPrice: 10,
      maxPrice: 59.9,
      inStock: true,
      sort: 'price-asc',
      page: 3,
    });
  });

  it('falls back to defaults for malformed values instead of failing', () => {
    expect(
      parseProductQuery({
        category: ['Bad Slug', 'ok-slug'],
        minPrice: '-3',
        maxPrice: 'abc',
        inStock: 'yes',
        sort: 'name',
        page: '0',
      }),
    ).toEqual({ ...emptyProductQuery, categories: ['ok-slug'] });
  });

  it('round-trips through router params, omitting defaults', () => {
    const query = parseProductQuery({ category: 'gear-x', inStock: 'true', page: '2' });

    const params = toQueryParams(query);

    expect(params).toEqual({
      q: null,
      category: ['gear-x'],
      brand: null,
      minPrice: null,
      maxPrice: null,
      inStock: 'true',
      sort: null,
      page: 2,
    });
    expect(toQueryParams(emptyProductQuery)['page']).toBeNull();
  });

  it('builds the API URL with the department and repeated filter keys', () => {
    const url = productsApiUrl(
      {
        ...emptyProductQuery,
        q: 'whey & co',
        categories: ['a', 'b'],
        minPrice: 0,
        sort: 'discount',
      },
      'supplements',
    );

    expect(url).toBe(
      '/api/catalog/products?department=supplements&q=whey+%26+co&category=a&category=b&minPrice=0&sort=discount&page=1&pageSize=24',
    );
  });

  it('knows when filters are active', () => {
    expect(hasActiveFilters(emptyProductQuery)).toBe(false);
    expect(hasActiveFilters({ ...emptyProductQuery, sort: 'newest', page: 4 })).toBe(false);
    expect(hasActiveFilters({ ...emptyProductQuery, maxPrice: 0 })).toBe(true);
  });
});
