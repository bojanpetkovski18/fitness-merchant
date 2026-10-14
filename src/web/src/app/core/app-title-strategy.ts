import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRouteSnapshot, RouterStateSnapshot, TitleStrategy } from '@angular/router';

export const siteName = 'Fitness Merchant';
export const defaultDescription =
  'Fitness Merchant: supplements and training gear in one modern storefront. A portfolio demo with fictional data.';

/**
 * Page titles read "Gear · Fitness Merchant" and each route can set a meta description in its data.
 * Pages whose title depends on loaded data (product, brand) update both once the data arrives.
 */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const pageTitle = this.buildTitle(snapshot);
    this.title.setTitle(pageTitle ? `${pageTitle} · ${siteName}` : siteName);
    this.meta.updateTag({
      name: 'description',
      content: deepestDescription(snapshot.root) ?? defaultDescription,
    });
  }
}

function deepestDescription(route: ActivatedRouteSnapshot): string | undefined {
  let description: string | undefined;
  for (let current: ActivatedRouteSnapshot | null = route; current; current = current.firstChild) {
    const value: unknown = current.data['description'];
    if (typeof value === 'string') {
      description = value;
    }
  }
  return description;
}
