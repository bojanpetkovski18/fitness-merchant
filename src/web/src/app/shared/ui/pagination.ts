import { Component, computed, input } from '@angular/core';
import { Params, RouterLink } from '@angular/router';

type PageItem = { kind: 'page'; page: number } | { kind: 'gap'; key: string };

/** Real links (not buttons) so pages are shareable and open in new tabs; the current page is aria-current. */
@Component({
  selector: 'app-pagination',
  imports: [RouterLink],
  template: `
    @if (pageCount() > 1) {
      <nav aria-label="Pagination" class="mt-8 flex flex-wrap items-center justify-center gap-1">
        @if (page() > 1) {
          <a
            [routerLink]="[]"
            [queryParams]="params(page() - 1)"
            queryParamsHandling="merge"
            class="rounded-md px-3 py-2 text-sm font-semibold hover:bg-surface-2"
            >‹ Previous<span class="sr-only"> page</span></a
          >
        }
        @for (item of items(); track item.kind === 'page' ? item.page : item.key) {
          @if (item.kind === 'page') {
            <a
              [routerLink]="[]"
              [queryParams]="params(item.page)"
              queryParamsHandling="merge"
              [attr.aria-current]="item.page === page() ? 'page' : null"
              [attr.aria-label]="'Page ' + item.page"
              class="grid min-w-10 place-items-center rounded-md px-2 py-2 text-sm font-semibold hover:bg-surface-2 aria-[current=page]:bg-accent aria-[current=page]:text-on-accent"
              >{{ item.page }}</a
            >
          } @else {
            <span aria-hidden="true" class="px-1 text-muted">…</span>
          }
        }
        @if (page() < pageCount()) {
          <a
            [routerLink]="[]"
            [queryParams]="params(page() + 1)"
            queryParamsHandling="merge"
            class="rounded-md px-3 py-2 text-sm font-semibold hover:bg-surface-2"
            >Next<span class="sr-only"> page</span> ›</a
          >
        }
      </nav>
    }
  `,
})
export class Pagination {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();

  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize())),
  );

  /** First, last, and two either side of the current page, with gaps. */
  protected readonly items = computed<PageItem[]>(() => {
    const last = this.pageCount();
    const current = Math.min(this.page(), last);
    const pages = new Set([1, last, current - 2, current - 1, current, current + 1, current + 2]);
    const sorted = [...pages].filter((p) => p >= 1 && p <= last).sort((a, b) => a - b);
    const result: PageItem[] = [];
    sorted.forEach((p, i) => {
      if (i > 0 && p - sorted[i - 1]! > 1) result.push({ kind: 'gap', key: `gap-${p}` });
      result.push({ kind: 'page', page: p });
    });
    return result;
  });

  protected params(page: number): Params {
    return { page: page > 1 ? page : null };
  }
}
