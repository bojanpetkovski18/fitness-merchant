import { Dialog } from '@angular/cdk/dialog';
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
  Department,
  PickedImage,
  departmentLabels,
} from '../../core/admin/admin.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { ToastService } from '../../core/ui/toast.service';
import { fieldError } from '../../shared/forms/field-error';
import { confirmAction } from '../../shared/ui/confirm-dialog';
import { EmptyState, ErrorState } from '../../shared/ui/states';
import { BusyButton } from '../../shared/ui/busy-button';
import { ImagePicker } from './image-picker';

const inputClass =
  'mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2.5 text-fg aria-invalid:border-danger';

/** Shared delete flow: confirm, then explain a 409 ("still has N products") in a toast. */
async function deleteWithConfirm(
  dialog: Dialog,
  http: HttpClient,
  toasts: ToastService,
  kind: 'category' | 'brand',
  item: { id: string; name: string; productCount: number },
): Promise<boolean> {
  if (item.productCount > 0) {
    // The server refuses too (409); saying so up front saves a pointless confirmation.
    toasts.error(
      `“${item.name}” still has ${item.productCount} ${item.productCount === 1 ? 'product' : 'products'}. Move or delete them first.`,
    );
    return false;
  }
  const confirmed = await confirmAction(dialog, {
    title: `Delete the ${kind} “${item.name}”?`,
    message: 'This cannot be undone.',
    confirmLabel: 'Delete',
    dismissLabel: 'Keep it',
  });
  if (!confirmed) return false;
  try {
    await firstValueFrom(
      http.delete(`/api/admin/${kind === 'category' ? 'categories' : 'brands'}/${item.id}`),
    );
    toasts.show(`The ${kind} “${item.name}” was deleted.`);
    return true;
  } catch (error) {
    toasts.error(toApiProblem(error).title);
    return true; // reload anyway: the count may have changed
  }
}

@Component({
  selector: 'app-admin-categories-page',
  imports: [RouterLink, EmptyState, ErrorState],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <h1 class="fm-display text-3xl md:text-4xl">Categories</h1>
      <a
        routerLink="/admin/categories/new"
        class="rounded-md bg-accent px-4 py-2.5 font-bold text-on-accent hover:bg-accent-strong"
        >New category</a
      >
    </div>
    <div class="mt-6">
      @if (categories.error()) {
        <app-error-state title="Couldn't load categories" (retry)="categories.reload()" />
      } @else if (categories.hasValue()) {
        @if (categories.value().length === 0) {
          <app-empty-state
            title="No categories yet"
            message="Create one to start adding products."
          />
        } @else {
          @for (group of groups(); track group.department) {
            <section [attr.aria-labelledby]="'dept-' + group.department" class="mb-6">
              <h2 [id]="'dept-' + group.department" class="mb-2 font-bold">{{ group.label }}</h2>
              <ul class="divide-y divide-border rounded-xl border border-border bg-surface">
                @for (c of group.items; track c.id) {
                  <li class="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
                    <div class="min-w-0">
                      <a
                        [routerLink]="['/admin/categories', c.id]"
                        class="font-semibold hover:text-accent"
                        >{{ c.name }}</a
                      >
                      <p class="text-xs text-muted">
                        <span class="font-mono">{{ c.slug }}</span> · {{ c.productCount }}
                        {{ c.productCount === 1 ? 'product' : 'products' }} · order
                        {{ c.sortOrder }}
                      </p>
                    </div>
                    <div class="flex gap-1">
                      <a
                        [routerLink]="['/admin/categories', c.id]"
                        class="rounded px-2 py-1 text-sm font-semibold hover:bg-surface-2"
                        >Edit<span class="sr-only"> {{ c.name }}</span></a
                      >
                      <button
                        type="button"
                        class="rounded px-2 py-1 text-sm font-semibold text-danger hover:bg-danger/10"
                        (click)="remove(c)"
                      >
                        Delete<span class="sr-only"> {{ c.name }}</span>
                      </button>
                    </div>
                  </li>
                }
              </ul>
            </section>
          }
        }
      } @else {
        <p role="status" class="text-muted">Loading categories…</p>
      }
    </div>
  `,
})
export class AdminCategoriesPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  protected readonly categories = httpResource<AdminCategory[]>(() => '/api/admin/categories');
  protected readonly groups = computed(() => {
    const all = this.categories.hasValue() ? this.categories.value() : [];
    return (['supplements', 'gear'] as const)
      .map((d) => ({
        department: d,
        label: departmentLabels[d],
        items: all.filter((c) => c.department === d),
      }))
      .filter((g) => g.items.length > 0);
  });

  protected async remove(category: AdminCategory): Promise<void> {
    if (await deleteWithConfirm(this.dialog, this.http, this.toasts, 'category', category)) {
      this.categories.reload();
    }
  }
}

@Component({
  selector: 'app-admin-category-form-page',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, ErrorState, BusyButton],
  template: `
    <a routerLink="/admin/categories" class="text-sm font-semibold text-muted hover:text-fg"
      >‹ Categories</a
    >
    @if (notFound()) {
      <div class="mt-4"><app-empty-state title="Category not found" /></div>
    } @else if (category.error()) {
      <div class="mt-4">
        <app-error-state title="Couldn't load this category" (retry)="category.reload()" />
      </div>
    } @else if (id() && !category.hasValue()) {
      <p role="status" class="mt-4 text-muted">Loading the category…</p>
    } @else {
      <h1 class="fm-display mt-2 text-3xl md:text-4xl">
        {{ id() ? 'Edit category' : 'New category' }}
      </h1>
      <div aria-live="assertive">
        @if (problem(); as problem) {
          <p
            role="alert"
            class="mt-6 rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm font-semibold text-danger"
          >
            {{ problem.status === 400 ? 'Please fix the highlighted fields.' : problem.title }}
          </p>
        }
      </div>
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="mt-6 max-w-xl space-y-5">
        @let deptError = error('department', 'Department');
        <div>
          <label for="c-department" class="block text-sm font-semibold">Department</label>
          <select
            id="c-department"
            formControlName="department"
            [attr.aria-invalid]="deptError ? 'true' : null"
            [attr.aria-describedby]="deptError ? 'c-department-error' : null"
            [class]="inputClass"
          >
            <option value="">Choose a department</option>
            <option value="supplements">Supplements</option>
            <option value="gear">Gear</option>
          </select>
          @if (deptError) {
            <p id="c-department-error" class="mt-1 text-sm text-danger">{{ deptError }}</p>
          }
        </div>
        @let nameError = error('name', 'Name');
        <div>
          <label for="c-name" class="block text-sm font-semibold">Name</label>
          <input
            id="c-name"
            formControlName="name"
            [attr.aria-invalid]="nameError ? 'true' : null"
            [attr.aria-describedby]="nameError ? 'c-name-error' : null"
            [class]="inputClass"
          />
          @if (nameError) {
            <p id="c-name-error" class="mt-1 text-sm text-danger">{{ nameError }}</p>
          }
        </div>
        @let slugError = error('slug', 'Slug');
        <div>
          <label for="c-slug" class="block text-sm font-semibold">Slug</label>
          <input
            id="c-slug"
            formControlName="slug"
            [attr.aria-invalid]="slugError ? 'true' : null"
            [attr.aria-describedby]="slugError ? 'c-slug-hint c-slug-error' : 'c-slug-hint'"
            [class]="inputClass + ' font-mono'"
          />
          <p id="c-slug-hint" class="mt-1 text-xs text-muted">
            Leave empty to make one from the name.
          </p>
          @if (slugError) {
            <p id="c-slug-error" class="mt-1 text-sm text-danger">{{ slugError }}</p>
          }
        </div>
        @let descriptionError = error('description', 'Description');
        <div>
          <label for="c-description" class="block text-sm font-semibold"
            >Description (optional)</label
          >
          <textarea
            id="c-description"
            formControlName="description"
            rows="3"
            [attr.aria-invalid]="descriptionError ? 'true' : null"
            [attr.aria-describedby]="descriptionError ? 'c-description-error' : null"
            [class]="inputClass"
          ></textarea>
          @if (descriptionError) {
            <p id="c-description-error" class="mt-1 text-sm text-danger">{{ descriptionError }}</p>
          }
        </div>
        @let orderError = error('sortOrder', 'Sort order');
        <div>
          <label for="c-sort" class="block text-sm font-semibold">Sort order</label>
          <input
            id="c-sort"
            type="number"
            inputmode="numeric"
            formControlName="sortOrder"
            [attr.aria-invalid]="orderError ? 'true' : null"
            [attr.aria-describedby]="orderError ? 'c-sort-hint c-sort-error' : 'c-sort-hint'"
            [class]="inputClass + ' max-w-32'"
          />
          <p id="c-sort-hint" class="mt-1 text-xs text-muted">
            Lower numbers come first in the menu.
          </p>
          @if (orderError) {
            <p id="c-sort-error" class="mt-1 text-sm text-danger">{{ orderError }}</p>
          }
        </div>
        <div class="flex flex-wrap gap-3">
          <button
            type="submit"
            [appBusy]="pending()"
            class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong aria-disabled:opacity-60"
          >
            {{ pending() ? 'Saving…' : id() ? 'Save changes' : 'Create category' }}
          </button>
          <a
            routerLink="/admin/categories"
            class="rounded-md border border-border px-5 py-3 font-bold hover:bg-surface-2"
            >Cancel</a
          >
        </div>
      </form>
    }
  `,
})
export class AdminCategoryFormPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly id = input<string>();

  protected readonly inputClass = inputClass;
  protected readonly category = httpResource<AdminCategory>(() =>
    this.id() ? `/api/admin/categories/${encodeURIComponent(this.id()!)}` : undefined,
  );
  protected readonly form = inject(FormBuilder).group({
    department: ['' as Department | '', [Validators.required]],
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    slug: ['', [Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    sortOrder: [0 as number | null, [Validators.min(0), Validators.max(1000)]],
  });
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);
  protected readonly notFound = computed(() => {
    const error = this.category.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    effect(() => {
      if (!this.category.hasValue()) return;
      const c = this.category.value();
      this.form.reset({
        department: c.department,
        name: c.name,
        slug: c.slug,
        description: c.description ?? '',
        sortOrder: c.sortOrder,
      });
    });
  }

  protected error(field: keyof typeof this.form.controls, label: string): string | null {
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
    if (this.form.invalid) {
      focusFirstInvalid(this.host, this.injector);
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      department: v.department || null,
      name: v.name ?? '',
      slug: v.slug ?? '',
      description: v.description ?? '',
      sortOrder: v.sortOrder ?? 0,
    };
    this.pending.set(true);
    try {
      const id = this.id();
      const saved = await firstValueFrom(
        id
          ? this.http.put<AdminCategory>(`/api/admin/categories/${encodeURIComponent(id)}`, body)
          : this.http.post<AdminCategory>('/api/admin/categories', body),
      );
      this.toasts.show(`The category “${saved.name}” was saved.`);
      await this.router.navigateByUrl('/admin/categories');
    } catch (error) {
      this.problem.set(toApiProblem(error));
      focusFirstInvalid(this.host, this.injector);
    } finally {
      this.pending.set(false);
    }
  }
}

@Component({
  selector: 'app-admin-brands-page',
  imports: [RouterLink, EmptyState, ErrorState],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <h1 class="fm-display text-3xl md:text-4xl">Brands</h1>
      <a
        routerLink="/admin/brands/new"
        class="rounded-md bg-accent px-4 py-2.5 font-bold text-on-accent hover:bg-accent-strong"
        >New brand</a
      >
    </div>
    <div class="mt-6">
      @if (brands.error()) {
        <app-error-state title="Couldn't load brands" (retry)="brands.reload()" />
      } @else if (brands.hasValue()) {
        @if (brands.value().length === 0) {
          <app-empty-state title="No brands yet" message="Create one to start adding products." />
        } @else {
          <ul class="divide-y divide-border rounded-xl border border-border bg-surface">
            @for (b of brands.value(); track b.id) {
              <li class="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
                <div class="flex min-w-0 items-center gap-3">
                  <span
                    aria-hidden="true"
                    class="size-6 shrink-0 rounded-full border border-border"
                    [style.background-color]="b.accentColor"
                  ></span>
                  <div class="min-w-0">
                    <a
                      [routerLink]="['/admin/brands', b.id]"
                      class="font-semibold hover:text-accent"
                      >{{ b.name }}</a
                    >
                    <p class="text-xs text-muted">
                      <span class="font-mono">{{ b.slug }}</span> · {{ b.productCount }}
                      {{ b.productCount === 1 ? 'product' : 'products' }}
                    </p>
                  </div>
                </div>
                <div class="flex gap-1">
                  <a
                    [routerLink]="['/admin/brands', b.id]"
                    class="rounded px-2 py-1 text-sm font-semibold hover:bg-surface-2"
                    >Edit<span class="sr-only"> {{ b.name }}</span></a
                  >
                  <button
                    type="button"
                    class="rounded px-2 py-1 text-sm font-semibold text-danger hover:bg-danger/10"
                    (click)="remove(b)"
                  >
                    Delete<span class="sr-only"> {{ b.name }}</span>
                  </button>
                </div>
              </li>
            }
          </ul>
        }
      } @else {
        <p role="status" class="text-muted">Loading brands…</p>
      }
    </div>
  `,
})
export class AdminBrandsPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  protected readonly brands = httpResource<AdminBrand[]>(() => '/api/admin/brands');

  protected async remove(brand: AdminBrand): Promise<void> {
    if (await deleteWithConfirm(this.dialog, this.http, this.toasts, 'brand', brand)) {
      this.brands.reload();
    }
  }
}

@Component({
  selector: 'app-admin-brand-form-page',
  imports: [ReactiveFormsModule, RouterLink, ImagePicker, EmptyState, ErrorState, BusyButton],
  template: `
    <a routerLink="/admin/brands" class="text-sm font-semibold text-muted hover:text-fg"
      >‹ Brands</a
    >
    @if (notFound()) {
      <div class="mt-4"><app-empty-state title="Brand not found" /></div>
    } @else if (brand.error()) {
      <div class="mt-4">
        <app-error-state title="Couldn't load this brand" (retry)="brand.reload()" />
      </div>
    } @else if (id() && !brand.hasValue()) {
      <p role="status" class="mt-4 text-muted">Loading the brand…</p>
    } @else {
      <h1 class="fm-display mt-2 text-3xl md:text-4xl">{{ id() ? 'Edit brand' : 'New brand' }}</h1>
      <div aria-live="assertive">
        @if (problem(); as problem) {
          <p
            role="alert"
            class="mt-6 rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm font-semibold text-danger"
          >
            {{ problem.status === 400 ? 'Please fix the highlighted fields.' : problem.title }}
          </p>
        }
      </div>
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="mt-6 max-w-xl space-y-5">
        @let nameError = error('name', 'Name');
        <div>
          <label for="b-name" class="block text-sm font-semibold">Name</label>
          <input
            id="b-name"
            formControlName="name"
            [attr.aria-invalid]="nameError ? 'true' : null"
            [attr.aria-describedby]="nameError ? 'b-name-error' : null"
            [class]="inputClass"
          />
          @if (nameError) {
            <p id="b-name-error" class="mt-1 text-sm text-danger">{{ nameError }}</p>
          }
        </div>
        @let slugError = error('slug', 'Slug');
        <div>
          <label for="b-slug" class="block text-sm font-semibold">Slug</label>
          <input
            id="b-slug"
            formControlName="slug"
            [attr.aria-invalid]="slugError ? 'true' : null"
            [attr.aria-describedby]="slugError ? 'b-slug-hint b-slug-error' : 'b-slug-hint'"
            [class]="inputClass + ' font-mono'"
          />
          <p id="b-slug-hint" class="mt-1 text-xs text-muted">
            Leave empty to make one from the name.
          </p>
          @if (slugError) {
            <p id="b-slug-error" class="mt-1 text-sm text-danger">{{ slugError }}</p>
          }
        </div>
        @let descriptionError = error('description', 'Description');
        <div>
          <label for="b-description" class="block text-sm font-semibold">Description</label>
          <textarea
            id="b-description"
            formControlName="description"
            rows="4"
            [attr.aria-invalid]="descriptionError ? 'true' : null"
            [attr.aria-describedby]="descriptionError ? 'b-description-error' : null"
            [class]="inputClass"
          ></textarea>
          @if (descriptionError) {
            <p id="b-description-error" class="mt-1 text-sm text-danger">{{ descriptionError }}</p>
          }
        </div>
        @let accentError = error('accentColor', 'Accent colour');
        <div>
          <label for="b-accent" class="block text-sm font-semibold">Accent colour</label>
          <div class="mt-1 flex items-center gap-3">
            <input
              id="b-accent"
              type="color"
              formControlName="accentColor"
              [attr.aria-invalid]="accentError ? 'true' : null"
              [attr.aria-describedby]="
                accentError ? 'b-accent-hint b-accent-error' : 'b-accent-hint'
              "
              class="h-10 w-16 cursor-pointer rounded-md border border-border bg-surface"
            />
            <span class="font-mono text-sm">{{ form.controls.accentColor.value }}</span>
          </div>
          <p id="b-accent-hint" class="mt-1 text-xs text-muted">
            Tints the brand's artwork and badges.
          </p>
          @if (accentError) {
            <p id="b-accent-error" class="mt-1 text-sm text-danger">{{ accentError }}</p>
          }
        </div>
        <app-image-picker
          label="Logo"
          [(value)]="logo"
          [serverError]="problem()?.fieldErrors?.['logoImageId']?.[0] ?? null"
        />
        <div class="flex flex-wrap gap-3">
          <button
            type="submit"
            [appBusy]="pending()"
            class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong aria-disabled:opacity-60"
          >
            {{ pending() ? 'Saving…' : id() ? 'Save changes' : 'Create brand' }}
          </button>
          <a
            routerLink="/admin/brands"
            class="rounded-md border border-border px-5 py-3 font-bold hover:bg-surface-2"
            >Cancel</a
          >
        </div>
      </form>
    }
  `,
})
export class AdminBrandFormPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly id = input<string>();

  protected readonly inputClass = inputClass;
  protected readonly brand = httpResource<AdminBrand>(() =>
    this.id() ? `/api/admin/brands/${encodeURIComponent(this.id()!)}` : undefined,
  );
  protected readonly form = inject(FormBuilder).group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    slug: ['', [Validators.maxLength(100)]],
    description: ['', [Validators.required, Validators.maxLength(1000)]],
    accentColor: ['#3b82f6', [Validators.required]],
  });
  protected readonly logo = signal<PickedImage | null>(null);
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);
  protected readonly notFound = computed(() => {
    const error = this.brand.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    effect(() => {
      if (!this.brand.hasValue()) return;
      const b = this.brand.value();
      this.form.reset({
        name: b.name,
        slug: b.slug,
        description: b.description,
        accentColor: b.accentColor,
      });
      this.logo.set(b.logoImageId && b.logoUrl ? { id: b.logoImageId, url: b.logoUrl } : null);
    });
  }

  protected error(field: keyof typeof this.form.controls, label: string): string | null {
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
    if (this.form.invalid) {
      focusFirstInvalid(this.host, this.injector);
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      name: v.name ?? '',
      slug: v.slug ?? '',
      description: v.description ?? '',
      accentColor: v.accentColor ?? '',
      logoImageId: this.logo()?.id ?? null,
    };
    this.pending.set(true);
    try {
      const id = this.id();
      const saved = await firstValueFrom(
        id
          ? this.http.put<AdminBrand>(`/api/admin/brands/${encodeURIComponent(id)}`, body)
          : this.http.post<AdminBrand>('/api/admin/brands', body),
      );
      this.toasts.show(`The brand “${saved.name}” was saved.`);
      await this.router.navigateByUrl('/admin/brands');
    } catch (error) {
      this.problem.set(toApiProblem(error));
      focusFirstInvalid(this.host, this.injector);
    } finally {
      this.pending.set(false);
    }
  }
}

/** Error attributes appear on the next render, so focus moves after it. */
function focusFirstInvalid(host: ElementRef<HTMLElement>, injector: Injector): void {
  afterNextRender(
    () => host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
    {
      injector,
    },
  );
}
