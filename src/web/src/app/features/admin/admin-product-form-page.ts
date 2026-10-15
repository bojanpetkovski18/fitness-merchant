import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AdminBrand,
  AdminCategory,
  AdminProduct,
  PickedImage,
  ProductInput,
  departmentLabels,
} from '../../core/admin/admin.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { ToastService } from '../../core/ui/toast.service';
import { fieldError } from '../../shared/forms/field-error';
import { EmptyState, ErrorState } from '../../shared/ui/states';
import { ImagePicker } from './image-picker';

type Field =
  | 'name'
  | 'sku'
  | 'slug'
  | 'description'
  | 'categoryId'
  | 'brandId'
  | 'price'
  | 'discountPercent'
  | 'stockQuantity';

const inputClass =
  'mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2.5 text-fg aria-invalid:border-danger';

/** Create (no id) or edit a product. Edits carry the version they started from; a stale one gets a 409. */
@Component({
  selector: 'app-admin-product-form-page',
  imports: [ReactiveFormsModule, RouterLink, ImagePicker, EmptyState, ErrorState],
  template: `
    <a routerLink="/admin/products" class="text-sm font-semibold text-muted hover:text-fg"
      >‹ Products</a
    >
    @if (notFound()) {
      <div class="mt-4">
        <app-empty-state title="Product not found" message="It may have been deleted." />
      </div>
    } @else if (product.error()) {
      <div class="mt-4">
        <app-error-state title="Couldn't load this product" (retry)="product.reload()" />
      </div>
    } @else if (id() && !product.hasValue()) {
      <p role="status" class="mt-4 text-muted">Loading the product…</p>
    } @else {
      <div class="mt-2 flex flex-wrap items-center gap-3">
        <h1 class="fm-display text-3xl md:text-4xl">{{ id() ? 'Edit product' : 'New product' }}</h1>
        @if (product.hasValue() && !product.value().isActive) {
          <span class="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-semibold text-muted"
            >Archived</span
          >
        }
      </div>
      @if (product.hasValue() && product.value().isActive) {
        <p class="mt-1 text-sm">
          <a
            [href]="'/products/' + product.value().slug"
            target="_blank"
            class="font-semibold text-accent underline underline-offset-4"
            >View in store<span class="sr-only"> (opens in a new tab)</span></a
          >
        </p>
      }

      <div aria-live="assertive">
        @if (conflict()) {
          <div
            role="alert"
            class="mt-6 rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm text-danger"
          >
            <p class="font-semibold">{{ conflict() }}</p>
            <button
              #reloadButton
              type="button"
              class="mt-2 rounded-md bg-danger px-3 py-1.5 font-bold text-bg"
              (click)="reloadLatest()"
            >
              Reload latest version
            </button>
            <p class="mt-1 text-xs">Your unsaved changes on this page will be replaced.</p>
          </div>
        } @else if (problem(); as problem) {
          <p
            role="alert"
            class="mt-6 rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm font-semibold text-danger"
          >
            {{ problem.status === 400 ? 'Please fix the highlighted fields.' : problem.title }}
          </p>
        }
      </div>
      <div aria-live="polite">
        @if (saved()) {
          <p
            role="status"
            class="mt-6 rounded-md border border-success/40 bg-success/10 px-4 py-3 text-sm font-semibold text-success"
          >
            Saved.
          </p>
        }
      </div>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="mt-6 max-w-3xl space-y-5">
        <div class="grid gap-5 md:grid-cols-2">
          @let nameError = error('name', 'Name');
          <div class="md:col-span-2">
            <label for="p-name" class="block text-sm font-semibold">Name</label>
            <input
              id="p-name"
              formControlName="name"
              [attr.aria-invalid]="nameError ? 'true' : null"
              [attr.aria-describedby]="nameError ? 'p-name-error' : null"
              [class]="inputClass"
            />
            @if (nameError) {
              <p id="p-name-error" class="mt-1 text-sm text-danger">{{ nameError }}</p>
            }
          </div>

          @let skuError = error('sku', 'SKU');
          <div>
            <label for="p-sku" class="block text-sm font-semibold">SKU</label>
            <input
              id="p-sku"
              formControlName="sku"
              autocapitalize="characters"
              [attr.aria-invalid]="skuError ? 'true' : null"
              [attr.aria-describedby]="skuError ? 'p-sku-error' : null"
              [class]="inputClass + ' font-mono uppercase'"
            />
            @if (skuError) {
              <p id="p-sku-error" class="mt-1 text-sm text-danger">{{ skuError }}</p>
            }
          </div>

          @let slugError = error('slug', 'Slug');
          <div>
            <label for="p-slug" class="block text-sm font-semibold">Slug</label>
            <input
              id="p-slug"
              formControlName="slug"
              [attr.aria-invalid]="slugError ? 'true' : null"
              [attr.aria-describedby]="slugError ? 'p-slug-hint p-slug-error' : 'p-slug-hint'"
              [class]="inputClass + ' font-mono'"
            />
            <p id="p-slug-hint" class="mt-1 text-xs text-muted">
              The page address. Leave empty to make one from the name.
            </p>
            @if (slugError) {
              <p id="p-slug-error" class="mt-1 text-sm text-danger">{{ slugError }}</p>
            }
          </div>

          @let descriptionError = error('description', 'Description');
          <div class="md:col-span-2">
            <label for="p-description" class="block text-sm font-semibold">Description</label>
            <textarea
              id="p-description"
              formControlName="description"
              rows="5"
              [attr.aria-invalid]="descriptionError ? 'true' : null"
              [attr.aria-describedby]="descriptionError ? 'p-description-error' : null"
              [class]="inputClass"
            ></textarea>
            @if (descriptionError) {
              <p id="p-description-error" class="mt-1 text-sm text-danger">
                {{ descriptionError }}
              </p>
            }
          </div>

          @let categoryError = error('categoryId', 'Category');
          <div>
            <label for="p-category" class="block text-sm font-semibold">Category</label>
            <select
              id="p-category"
              formControlName="categoryId"
              [attr.aria-invalid]="categoryError ? 'true' : null"
              [attr.aria-describedby]="categoryError ? 'p-category-error' : null"
              [class]="inputClass"
            >
              <option value="">Choose a category</option>
              @for (group of categoryGroups(); track group.label) {
                <optgroup [label]="group.label">
                  @for (c of group.items; track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </optgroup>
              }
            </select>
            @if (categoryError) {
              <p id="p-category-error" class="mt-1 text-sm text-danger">{{ categoryError }}</p>
            }
          </div>

          @let brandError = error('brandId', 'Brand');
          <div>
            <label for="p-brand" class="block text-sm font-semibold">Brand</label>
            <select
              id="p-brand"
              formControlName="brandId"
              [attr.aria-invalid]="brandError ? 'true' : null"
              [attr.aria-describedby]="brandError ? 'p-brand-error' : null"
              [class]="inputClass"
            >
              <option value="">Choose a brand</option>
              @if (brands.hasValue()) {
                @for (b of brands.value(); track b.id) {
                  <option [value]="b.id">{{ b.name }}</option>
                }
              }
            </select>
            @if (brandError) {
              <p id="p-brand-error" class="mt-1 text-sm text-danger">{{ brandError }}</p>
            }
          </div>
        </div>

        <div class="grid gap-5 sm:grid-cols-3">
          @let priceError = error('price', 'Price');
          <div>
            <label for="p-price" class="block text-sm font-semibold">Price (€)</label>
            <input
              id="p-price"
              type="number"
              inputmode="decimal"
              step="0.01"
              min="0.01"
              formControlName="price"
              [attr.aria-invalid]="priceError ? 'true' : null"
              [attr.aria-describedby]="priceError ? 'p-price-error' : null"
              [class]="inputClass"
            />
            @if (priceError) {
              <p id="p-price-error" class="mt-1 text-sm text-danger">{{ priceError }}</p>
            }
          </div>

          @let discountError = error('discountPercent', 'Discount');
          <div>
            <label for="p-discount" class="block text-sm font-semibold">Discount (%)</label>
            <input
              id="p-discount"
              type="number"
              inputmode="numeric"
              min="0"
              max="90"
              formControlName="discountPercent"
              [attr.aria-invalid]="discountError ? 'true' : null"
              [attr.aria-describedby]="discountError ? 'p-discount-error' : null"
              [class]="inputClass"
            />
            @if (discountError) {
              <p id="p-discount-error" class="mt-1 text-sm text-danger">{{ discountError }}</p>
            }
          </div>

          @let stockError = error('stockQuantity', 'Stock');
          <div>
            <label for="p-stock" class="block text-sm font-semibold">Stock</label>
            <input
              id="p-stock"
              type="number"
              inputmode="numeric"
              min="0"
              formControlName="stockQuantity"
              [attr.aria-invalid]="stockError ? 'true' : null"
              [attr.aria-describedby]="stockError ? 'p-stock-error' : null"
              [class]="inputClass"
            />
            @if (stockError) {
              <p id="p-stock-error" class="mt-1 text-sm text-danger">{{ stockError }}</p>
            }
          </div>
        </div>

        <app-image-picker
          label="Product image"
          [(value)]="image"
          [serverError]="problem()?.fieldErrors?.['imageId']?.[0] ?? null"
        />

        <label class="flex items-start gap-2">
          <input type="checkbox" formControlName="isActive" class="mt-1 size-4 accent-accent" />
          <span>
            <span class="font-semibold">In store</span>
            <span class="block text-sm text-muted"
              >Unticked, the product is archived: hidden from the store, kept on past orders.</span
            >
          </span>
        </label>

        <div class="flex flex-wrap gap-3">
          <button
            type="submit"
            [disabled]="pending()"
            class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong disabled:opacity-60"
          >
            {{ pending() ? 'Saving…' : id() ? 'Save changes' : 'Create product' }}
          </button>
          <a
            routerLink="/admin/products"
            class="rounded-md border border-border px-5 py-3 font-bold hover:bg-surface-2"
            >Cancel</a
          >
        </div>
      </form>
    }
  `,
})
export class AdminProductFormPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** Route parameter; absent on /admin/products/new. */
  readonly id = input<string>();

  protected readonly inputClass = inputClass;
  protected readonly product = httpResource<AdminProduct>(() =>
    this.id() ? `/api/admin/products/${encodeURIComponent(this.id()!)}` : undefined,
  );
  protected readonly categories = httpResource<AdminCategory[]>(() => '/api/admin/categories');
  protected readonly brands = httpResource<AdminBrand[]>(() => '/api/admin/brands');

  protected readonly form = inject(FormBuilder).group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    sku: ['', [Validators.required, Validators.maxLength(32)]],
    slug: ['', [Validators.maxLength(140)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    categoryId: ['', [Validators.required]],
    brandId: ['', [Validators.required]],
    price: [
      null as number | null,
      [Validators.required, Validators.min(0.01), Validators.max(99999.99)],
    ],
    discountPercent: [0 as number | null, [Validators.min(0), Validators.max(90)]],
    stockQuantity: [
      0 as number | null,
      [Validators.required, Validators.min(0), Validators.max(100000)],
    ],
    isActive: [true],
  });
  protected readonly image = signal<PickedImage | null>(null);
  private version: number | null = null;

  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly saved = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);
  protected readonly conflict = signal<string | null>(null);

  protected readonly notFound = computed(() => {
    const error = this.product.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected readonly categoryGroups = computed(() => {
    const all = this.categories.hasValue() ? this.categories.value() : [];
    return (['supplements', 'gear'] as const).map((d) => ({
      label: departmentLabels[d],
      items: all.filter((c) => c.department === d),
    }));
  });

  constructor() {
    // Whenever a version of the product arrives (first load, save, reload after a conflict), the form shows it.
    effect(() => {
      if (!this.product.hasValue()) return;
      const p = this.product.value();
      this.form.reset({
        name: p.name,
        sku: p.sku,
        slug: p.slug,
        description: p.description,
        categoryId: p.categoryId,
        brandId: p.brandId,
        price: p.price,
        discountPercent: p.discountPercent,
        stockQuantity: p.stockQuantity,
        isActive: p.isActive,
      });
      this.image.set(p.imageId && p.imageUrl ? { id: p.imageId, url: p.imageUrl } : null);
      this.version = p.version;
    });
  }

  protected error(field: Field, label: string): string | null {
    return fieldError(
      this.form.controls[field],
      label,
      this.problem()?.fieldErrors[field],
      this.submitted(),
    );
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.problem.set(null);
    this.conflict.set(null);
    this.saved.set(false);
    if (this.form.invalid) {
      this.focusFirstInvalid();
      return;
    }

    const v = this.form.getRawValue();
    const body: ProductInput = {
      sku: v.sku ?? '',
      name: v.name ?? '',
      slug: v.slug ?? '',
      description: v.description ?? '',
      categoryId: v.categoryId || null,
      brandId: v.brandId || null,
      price: v.price,
      discountPercent: v.discountPercent ?? 0,
      stockQuantity: v.stockQuantity,
      isActive: v.isActive ?? true,
      imageId: this.image()?.id ?? null,
      version: this.version,
    };

    this.pending.set(true);
    try {
      const id = this.id();
      if (id) {
        const updated = await firstValueFrom(
          this.http.put<AdminProduct>(`/api/admin/products/${encodeURIComponent(id)}`, body),
        );
        this.product.set(updated);
        this.submitted.set(false);
        this.saved.set(true);
      } else {
        const created = await firstValueFrom(
          this.http.post<AdminProduct>('/api/admin/products', body),
        );
        this.toasts.show(`“${created.name}” was created.`, {
          action: created.isActive
            ? { label: 'View in store', link: `/products/${created.slug}` }
            : undefined,
        });
        await this.router.navigateByUrl('/admin/products');
      }
    } catch (error) {
      const problem = toApiProblem(error);
      if (problem.status === 409) {
        this.conflict.set(problem.title);
        afterNextRender(
          () =>
            this.host.nativeElement.querySelector<HTMLElement>('[role="alert"] button')?.focus(),
          { injector: this.injector },
        );
      } else {
        this.problem.set(problem);
        this.focusFirstInvalid();
      }
    } finally {
      this.pending.set(false);
    }
  }

  protected reloadLatest(): void {
    this.conflict.set(null);
    this.submitted.set(false);
    this.product.reload();
  }

  private focusFirstInvalid(): void {
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
      { injector: this.injector },
    );
  }
}
