import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, TitleStrategy, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { App } from './app';
import { routes } from './app.routes';
import { AppTitleStrategy } from './core/app-title-strategy';
import { MetaStore } from './core/meta.store';

/** No HTTP for deployment facts in these tests: a fixed answer instead. */
function metaStub(demoMode = false, resetIntervalHours: number | null = null) {
  return {
    provide: MetaStore,
    useValue: { demoMode: signal(demoMode), resetIntervalHours: signal(resetIntervalHours) },
  };
}

describe('App shell', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        { provide: TitleStrategy, useClass: AppTitleStrategy },
        provideHttpClient(),
        provideHttpClientTesting(),
        metaStub(),
      ],
    });
  });

  it('shows the demo banner only in demo mode', async () => {
    TestBed.overrideProvider(MetaStore, {
      useValue: { demoMode: signal(true), resetIntervalHours: signal(6) },
    });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const banner = (fixture.nativeElement as HTMLElement).querySelector('app-demo-banner');
    expect(banner?.textContent).toContain('Demo store');
    expect(banner?.textContent).toContain('resets every 6 hours');
  });

  it('renders the skip link, main landmark, primary navigation and footer', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('a[href="#main"]')?.textContent).toContain('Skip to content');
    expect(element.querySelector('main#main')).not.toBeNull();

    const nav = element.querySelector('nav[aria-label="Main"]');
    const labels = Array.from(nav?.querySelectorAll('a') ?? []).map((a) => a.textContent?.trim());
    expect(labels).toEqual(['Home', 'Supplements', 'Gear', 'Brands']);

    expect(element.querySelector('footer')).not.toBeNull();
  });

  it('moves focus to main when the path changes, but not for query-only changes (e.g. filters)', async () => {
    const fixture = TestBed.createComponent(App);
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/cart');
    await fixture.whenStable();

    const footerLink = element.querySelector<HTMLAnchorElement>('footer a')!;
    footerLink.focus();
    await router.navigateByUrl('/cart?category=protein');
    await fixture.whenStable();
    expect(document.activeElement).toBe(footerLink);

    await router.navigateByUrl('/checkout-does-not-exist');
    await fixture.whenStable();
    expect(document.activeElement).toBe(element.querySelector('main'));
    element.remove();
  });

  it('skip link moves focus to the main content', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);

    element.querySelector<HTMLAnchorElement>('a[href="#main"]')?.click();

    expect(document.activeElement).toBe(element.querySelector('main'));
    element.remove();
  });
});

describe('Routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        { provide: TitleStrategy, useClass: AppTitleStrategy },
        provideHttpClient(),
        provideHttpClientTesting(),
        metaStub(),
      ],
    });
  });

  it('renders the 404 page for an unknown URL', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/no-such-page');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toContain(
      'Page not found',
    );
    expect(TestBed.inject(Title).getTitle()).toBe('Page not found · Fitness Merchant');
  });

  it('renders the About page with the AI-assistance note and the repository link', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/about');

    const page = harness.routeNativeElement;
    expect(page?.querySelector('h1')?.textContent).toContain('A portfolio project, not a shop');
    expect(page?.textContent).toContain('made with AI assistance');
    expect(
      page?.querySelector('a[href="https://github.com/bojanpetkovski18/fitness-merchant"]'),
    ).not.toBeNull();
    expect(TestBed.inject(Title).getTitle()).toBe('About · Fitness Merchant');
  });

  it('opens and closes FAQ answers with buttons that report their state', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/contact');
    const page = harness.routeNativeElement!;
    const button = page.querySelector<HTMLButtonElement>('#faq-button-pay')!;
    const panel = page.querySelector<HTMLElement>('#faq-panel-pay')!;

    expect([button.getAttribute('aria-expanded'), panel.hidden]).toEqual(['false', true]);
    expect(button.getAttribute('aria-controls')).toBe('faq-panel-pay');
    button.click();
    await harness.fixture.whenStable();
    expect([button.getAttribute('aria-expanded'), panel.hidden]).toEqual(['true', false]);
    expect(panel.textContent).toContain('4242 4242 4242 4242');
  });
});
