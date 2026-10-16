// Load test for the catalog list API (SPEC section 9: p95 < 200 ms locally with the seeded dataset).
// Run against a local API with the seed data:
//   k6 run tools/load/catalog-list.js                      (defaults: http://localhost:5080, 10 users, 60 s)
//   k6 run -e BASE_URL=http://localhost:5080 -e VUS=20 -e DURATION=30s tools/load/catalog-list.js
// k6 v2.3.0, pinned and checksum-verified when measured (PROGRESS.md -> Measured results).
import http from 'k6/http';
import { check } from 'k6';

const base = __ENV.BASE_URL || 'http://localhost:5080';

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '60s',
  thresholds: {
    'http_req_duration{name:catalog-list}': ['p(95)<200'],
    http_req_failed: ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

// What shoppers do on the listing pages: browse a department, narrow it, search, sort and page.
const queries = [
  'department=supplements',
  'department=gear',
  'department=supplements&category=protein',
  'department=gear&brand=brakken-barbell-co&inStock=true',
  'department=supplements&minPrice=10&maxPrice=40&sort=price-asc',
  'department=gear&sort=rating',
  'department=supplements&sort=discount&page=2',
  'q=whey',
  'q=kettlebel', // fuzzy (trigram) match
  'q=shaker&department=gear',
  'department=supplements&sort=newest&pageSize=48',
];

export default function () {
  const query = queries[Math.floor(Math.random() * queries.length)];
  const response = http.get(`${base}/api/catalog/products?${query}`, { tags: { name: 'catalog-list' } });
  check(response, {
    'status 200': (r) => r.status === 200,
    'has items': (r) => Array.isArray(r.json('items')),
  });
}
