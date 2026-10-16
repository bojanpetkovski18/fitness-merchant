import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import {
  Component,
  TemplateRef,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';
import { siteName } from '../../core/app-title-strategy';
import {
  BrandInfo,
  Department,
  DepartmentInfo,
  Page,
  ProductCard,
} from '../../core/catalog/catalog.models';
import {
  ProductQuery,
  emptyProductQuery,
  hasActiveFilters,
  parseProductQuery,
  productsApiUrl,
  sortOptions,
  toQueryParams,
} from '../../core/catalog/product-query';
import { ProductGrid } from '../../shared/catalog/product-grid';
import { Pagination } from '../../shared/ui/pagination';
import { EmptyState, ErrorState, ProductGridSkeleton } from '../../shared/ui/states';
import { FilterPanel } from './filter-panel';

const departmentCopy: Record<Department, { title: string; intro: string }> = {
  supplements: {
    title: 'Supplements',
    intro: 'Protein, pre-workout, creatine and everyday vitamins.',
  },
  gear: {
    title: 'Gear',
    intro: 'Strength and cardio equipment, apparel and training accessories.',
  },
};

@Component({
  selector: 'app-listing-page',
  imports: [FilterPanel, ProductGrid, ProductGridSkeleton, ErrorState, EmptyState, Pagination],
  templateUrl: './listing-page.html',
})
export class ListingPage {
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);
  private drawer: DialogRef | null = null;

  /** From route data (withComponentInputBinding). */
  readonly department = input.required<Department>();

  protected readonly copy = computed(() => departmentCopy[this.department()]);
  protected readonly sortOptions = sortOptions;
  protected readonly query = toSignal(
    inject(ActivatedRoute).queryParams.pipe(map(parseProductQuery)),
    { initialValue: emptyProductQuery },
  );

  protected readonly products = httpResource<Page<ProductCard>>(() =>
    productsApiUrl(this.query(), this.department()),
  );
  private readonly departments = httpResource<DepartmentInfo[]>(() => '/api/catalog/departments');
  /** Brands with products in this department only (K8). */
  protected readonly brands = httpResource<BrandInfo[]>(() => ({
    url: '/api/catalog/brands',
    params: { department: this.department() },
  }));
  protected readonly categories = computed(
    () =>
      (this.departments.hasValue() ? this.departments.value() : []).find(
        (d) => d.slug === this.department(),
      )?.categories ?? [],
  );

  protected readonly brandList = computed(() =>
    this.brands.hasValue() ? this.brands.value() : [],
  );
  protected readonly totalCount = computed(() =>
    this.products.hasValue() ? this.products.value().totalCount : null,
  );

  protected readonly filtersActive = computed(() => hasActiveFilters(this.query()));
  protected readonly activeFilterCount = computed(() => {
    const q = this.query();
    return (
      q.categories.length +
      q.brands.length +
      (q.minPrice !== null ? 1 : 0) +
      (q.maxPrice !== null ? 1 : 0) +
      (q.inStock ? 1 : 0)
    );
  });
  protected readonly badRequest = computed(() => {
    const error = this.products.error();
    return error instanceof HttpErrorResponse && error.status === 400;
  });
  protected readonly summary = computed(() => {
    // value() throws while a resource is in its error state, so every read outside a hasValue() branch is guarded.
    const page = this.products.hasValue() ? this.products.value() : undefined;
    if (!page || page.totalCount === 0) return '';
    const first = (page.page - 1) * page.pageSize + 1;
    const last = Math.min(page.page * page.pageSize, page.totalCount);
    return `Showing ${first}–${last} of ${page.totalCount} products`;
  });

  /** Follows the URL (back/forward) but is editable; typing updates the URL after a pause. */
  protected readonly searchText = linkedSignal(() => this.query().q);

  constructor() {
    toObservable(this.searchText)
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((text) => {
        if (text.trim() !== this.query().q) {
          // Typing should not flood the history with one entry per keystroke.
          this.update({ q: text.trim(), sort: null }, { replaceUrl: true });
        }
      });

    const meta = inject(Meta);
    effect(() => {
      const copy = this.copy();
      meta.updateTag({
        name: 'description',
        content: `${copy.title} at ${siteName}: ${copy.intro}`,
      });
    });
  }

  protected update(
    patch: Partial<ProductQuery>,
    options: { replaceUrl?: boolean; keepPage?: boolean } = {},
  ): void {
    const next: ProductQuery = {
      ...this.query(),
      ...patch,
      page: options.keepPage ? this.query().page : 1,
    };
    void this.router.navigate([], {
      queryParams: toQueryParams(next),
      replaceUrl: options.replaceUrl ?? false,
    });
  }

  protected clearFilters(): void {
    this.searchText.set('');
    void this.router.navigate([], {
      queryParams: toQueryParams({ ...emptyProductQuery, sort: this.query().sort }),
    });
  }

  protected setSort(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.update({ sort: sortOptions.find((o) => o.value === value)?.value ?? null });
  }

  protected openFilters(template: TemplateRef<unknown>): void {
    this.drawer = this.dialog.open(template, {
      ariaLabelledBy: 'filter-drawer-title',
      // A modal drawer: CDK no longer sets aria-modal by default, so it is explicit here.
      ariaModal: true,
      positionStrategy: this.overlay.position().global().left('0').top('0'),
      height: '100dvh',
      width: 'min(90vw, 22rem)',
      restoreFocus: true,
    });
  }

  protected closeFilters(): void {
    this.drawer?.close();
  }
}
