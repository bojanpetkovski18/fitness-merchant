import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** Admin area: a labelled navigation landmark (tabs on phones, a side list from md) around the current section. */
@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="mx-auto max-w-7xl px-4 py-8 md:py-10">
      <p class="text-sm font-semibold uppercase tracking-widest text-accent">Admin</p>
      <div class="mt-3 grid gap-6 md:grid-cols-[11rem_1fr] md:gap-8">
        <nav aria-label="Admin">
          <ul
            class="flex gap-1 overflow-x-auto border-b border-border md:flex-col md:border-b-0 md:border-r md:pr-4"
          >
            @for (link of links; track link.path) {
              <li>
                <a
                  [routerLink]="link.path"
                  routerLinkActive
                  [ariaCurrentWhenActive]="true"
                  class="-mb-px block border-b-2 border-transparent px-3 py-2 text-sm font-semibold whitespace-nowrap text-muted hover:text-fg md:mb-0 md:rounded-md md:border-b-0 aria-[current=true]:border-accent aria-[current=true]:text-fg md:aria-[current=true]:bg-surface-2"
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
  // Orders and the dashboard join this list in M10.
  protected readonly links = [
    { path: '/admin/products', label: 'Products' },
    { path: '/admin/categories', label: 'Categories' },
    { path: '/admin/brands', label: 'Brands' },
  ] as const;
}
