import { Component, ElementRef, inject, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { SiteFooter } from './core/layout/site-footer';
import { SiteHeader } from './core/layout/site-header';
import { ToastHost } from './core/ui/toast-host';
import { DemoBanner } from './core/layout/demo-banner';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, DemoBanner, SiteHeader, SiteFooter, ToastHost],
  templateUrl: './app.html',
})
export class App {
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  constructor() {
    // After navigating to another page, move focus to the content so keyboard and screen-reader
    // users start at the new page instead of the link they activated. Only when the path changes:
    // query-only changes (filters, sort, paging) must keep focus where the user is working.
    // The first load is skipped.
    let previousPath: string | null = null;
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => {
        const path = event.urlAfterRedirects.split(/[?#]/)[0];
        if (previousPath !== null && path !== previousPath) {
          this.focusMain();
        }
        previousPath = path ?? null;
      });
  }

  protected skipToContent(event: Event): void {
    // A plain "#main" href would navigate to "/#main" because of <base href="/">.
    event.preventDefault();
    this.focusMain();
  }

  private focusMain(): void {
    this.main().nativeElement.focus({ preventScroll: true });
  }
}
