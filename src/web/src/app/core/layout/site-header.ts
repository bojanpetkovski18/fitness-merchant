import { A11yModule } from '@angular/cdk/a11y';
import { BreakpointObserver } from '@angular/cdk/layout';
import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { CartStore } from '../cart/cart.store';
import { ThemeService } from '../theme/theme.service';

interface NavLink {
  readonly label: string;
  readonly path: string;
  readonly exact: boolean;
}

/** Matches Tailwind's `md` breakpoint, where the inline nav replaces the mobile menu. */
const desktopQuery = '(min-width: 48rem)';

@Component({
  selector: 'app-site-header',
  imports: [A11yModule, CdkMenu, CdkMenuItem, CdkMenuTrigger, RouterLink, RouterLinkActive],
  templateUrl: './site-header.html',
  host: {
    '(document:keydown.escape)': 'closeMenu(true)',
  },
})
export class SiteHeader {
  protected readonly theme = inject(ThemeService);
  protected readonly session = inject(SessionStore);
  protected readonly cart = inject(CartStore);
  /** The badge is decorative; the link name carries the count for screen readers. */
  protected readonly cartLabel = computed(() => {
    const count = this.cart.itemCount();
    return count === 0 ? 'Cart, empty' : `Cart, ${count} ${count === 1 ? 'item' : 'items'}`;
  });
  private readonly router = inject(Router);
  protected readonly menuOpen = signal(false);
  private readonly menuButton = viewChild.required<ElementRef<HTMLButtonElement>>('menuButton');
  private readonly mobileMenu = viewChild<ElementRef<HTMLElement>>('mobileMenu');
  private readonly injector = inject(Injector);

  protected readonly links: readonly NavLink[] = [
    { label: 'Home', path: '/', exact: true },
    { label: 'Supplements', path: '/supplements', exact: false },
    { label: 'Gear', path: '/gear', exact: false },
    { label: 'Brands', path: '/brands', exact: false },
  ];

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.menuOpen.set(false));

    inject(BreakpointObserver)
      .observe(desktopQuery)
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        if (state.matches) {
          this.menuOpen.set(false);
        }
      });
  }

  protected initials(name: string): string {
    return name
      .split(/\s+/)
      .filter((part) => part.length > 0)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }

  protected async signOut(): Promise<void> {
    this.menuOpen.set(false);
    await this.session.signOut();
    await this.router.navigateByUrl('/');
  }

  protected toggleMenu(): void {
    if (this.menuOpen()) {
      this.closeMenu(true);
      return;
    }
    this.menuOpen.set(true);
    // Focus is moved explicitly rather than via cdkTrapFocusAutoCapture: auto-capture restores focus
    // to whatever was focused at open time when the trap is destroyed, which overrode the return to
    // the menu button (Escape) and to <main> (navigation).
    afterNextRender(
      { write: () => this.mobileMenu()?.nativeElement.querySelector<HTMLElement>('a')?.focus() },
      { injector: this.injector },
    );
  }

  protected closeMenu(returnFocus: boolean): void {
    if (!this.menuOpen()) {
      return;
    }
    this.menuOpen.set(false);
    if (returnFocus) {
      this.menuButton().nativeElement.focus();
    }
  }
}
