import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface AccountLink {
  readonly path: string;
  readonly label: string;
  /** Orders stays highlighted on an order's detail page, where it is the section, not the page. */
  readonly exact: boolean;
}

/** Account area: a navigation landmark (tabs on phones, a side list from md) around the current section. */
@Component({
  selector: 'app-account-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="mx-auto max-w-5xl px-4 py-8 md:py-10">
      <div class="grid gap-6 md:grid-cols-[11rem_1fr] md:gap-10">
        <nav aria-label="Account">
          <ul
            class="flex gap-1 border-b border-border md:flex-col md:border-b-0 md:border-r md:pr-4"
          >
            @for (link of links; track link.path) {
              <li>
                <a
                  [routerLink]="link.path"
                  routerLinkActive
                  [routerLinkActiveOptions]="{ exact: link.exact }"
                  [ariaCurrentWhenActive]="link.exact ? 'page' : true"
                  class="-mb-px block border-b-2 border-transparent px-3 py-2 text-sm font-semibold text-muted hover:text-fg md:mb-0 md:rounded-md md:border-b-0 aria-[current=page]:border-accent aria-[current=page]:text-fg aria-[current=true]:border-accent aria-[current=true]:text-fg md:aria-[current=page]:bg-surface-2 md:aria-[current=true]:bg-surface-2"
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
export class AccountLayout {
  protected readonly links: readonly AccountLink[] = [
    { path: '/account', label: 'Profile', exact: true },
    { path: '/account/password', label: 'Password', exact: true },
    { path: '/account/orders', label: 'Orders', exact: false },
  ];
}
