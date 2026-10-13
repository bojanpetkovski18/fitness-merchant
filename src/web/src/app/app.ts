import { Component, ElementRef, inject, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, skip } from 'rxjs';
import { SiteFooter } from './core/layout/site-footer';
import { SiteHeader } from './core/layout/site-header';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SiteHeader, SiteFooter],
  templateUrl: './app.html',
})
export class App {
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  constructor() {
    // After client-side navigation, move focus to the content so keyboard and screen-reader
    // users start at the new page instead of the link they activated. Skip the first load.
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        skip(1),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.focusMain());
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
