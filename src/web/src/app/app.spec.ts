import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { TitleStrategy, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { App } from './app';
import { routes } from './app.routes';
import { AppTitleStrategy } from './core/app-title-strategy';

describe('App shell', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        { provide: TitleStrategy, useClass: AppTitleStrategy },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
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

  it('renders a planned page with its heading and milestone from route data', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/gear');

    const page = harness.routeNativeElement;
    expect(page?.querySelector('h1')?.textContent).toContain('Gear');
    expect(page?.textContent).toContain('Coming in M04');
    expect(TestBed.inject(Title).getTitle()).toBe('Gear · Fitness Merchant');
  });
});
