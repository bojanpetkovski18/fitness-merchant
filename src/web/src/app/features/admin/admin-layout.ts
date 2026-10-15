import { Component } from '@angular/core';
import { IsActiveMatchOptions, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** Admin area: a labelled navigation landmark around the current section. Tabs up to tablets, a side list from lg: below that the tables need the full width. */
@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 md:py-10">
      <p class="text-sm font-semibold uppercase tracking-widest text-accent">Admin</p>
      <div class="mt-3 grid gap-6 lg:grid-cols-[11rem_1fr] lg:gap-8">
        <nav aria-label="Admin">
          <ul
            class="flex gap-1 overflow-x-auto border-b border-border lg:flex-col lg:border-b-0 lg:border-r lg:pr-4"
          >
            @for (link of links; track link.path) {
              <li>
                <a
                  [routerLink]="link.path"
                  routerLinkActive
                  [routerLinkActiveOptions]="link.exact ? exactPath : withinPath"
                  [ariaCurrentWhenActive]="true"
                  class="-mb-px block border-b-2 border-transparent px-3 py-2 text-sm font-semibold whitespace-nowrap text-muted hover:text-fg lg:mb-0 lg:rounded-md lg:border-b-0 aria-[current=true]:border-accent aria-[current=true]:text-fg lg:aria-[current=true]:bg-surface-2"
                  >{{ link.label }}</a
                >
              </li>
            }
          </ul>
        </nav>
        <div class="min-w-0"><router-outlet /></div>
      </div>
    </div>
  `,
})
export class AdminLayout {
  // Query strings (filters, date ranges) never change which section is current.
  protected readonly exactPath: IsActiveMatchOptions = {
    paths: 'exact',
    queryParams: 'ignored',
    matrixParams: 'ignored',
    fragment: 'ignored',
  };
  protected readonly withinPath: IsActiveMatchOptions = {
    paths: 'subset',
    queryParams: 'ignored',
    matrixParams: 'ignored',
    fragment: 'ignored',
  };

  /** The dashboard lives at /admin itself, so only an exact match marks it. */
  protected readonly links = [
    { path: '/admin', label: 'Dashboard', exact: true },
    { path: '/admin/orders', label: 'Orders', exact: false },
    { path: '/admin/products', label: 'Products', exact: false },
    { path: '/admin/categories', label: 'Categories', exact: false },
    { path: '/admin/brands', label: 'Brands', exact: false },
  ] as const;
}
