import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SiteHeader } from './site-header';

describe('SiteHeader', () => {
  let fixture: ComponentFixture<SiteHeader>;
  let element: HTMLElement;

  const menuButton = () =>
    element.querySelector<HTMLButtonElement>('button[aria-controls="mobile-menu"]')!;

  beforeEach(async () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [SiteHeader],
      providers: [provideRouter([])],
    });
    fixture = TestBed.createComponent(SiteHeader);
    element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    await fixture.whenStable();
  });

  afterEach(() => element.remove());

  it('opens the mobile menu and reports it as expanded', async () => {
    menuButton().click();
    await fixture.whenStable();

    expect(menuButton().getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('#mobile-menu')).not.toBeNull();
  });

  it('closes the mobile menu on Escape and returns focus to the menu button', async () => {
    menuButton().click();
    await fixture.whenStable();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();

    expect(element.querySelector('#mobile-menu')).toBeNull();
    expect(menuButton().getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(menuButton());
  });

  it('returns focus to the menu button on Escape even if another control had focus when it opened', async () => {
    // Regression: the focus trap's auto-capture restored focus to the previously focused element
    // after closing, overriding the explicit return to the menu button.
    element.querySelector<HTMLAnchorElement>('a[aria-label="Cart"]')!.focus();
    menuButton().click();
    await fixture.whenStable();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();

    expect(document.activeElement).toBe(menuButton());
  });

  it('moves focus to the first menu link when the menu opens', async () => {
    menuButton().click();
    await fixture.whenStable();

    expect(document.activeElement?.textContent?.trim()).toBe('Home');
    expect(element.querySelector('#mobile-menu')?.contains(document.activeElement)).toBe(true);
  });

  it('theme toggle flips the theme on <html> and updates its accessible label', async () => {
    const toggle = () => element.querySelector<HTMLButtonElement>('button[aria-label*="theme"]')!;
    const themeBefore = document.documentElement.dataset['theme'];
    const labelBefore = toggle().getAttribute('aria-label');

    toggle().click();
    await fixture.whenStable();

    const themeAfter = document.documentElement.dataset['theme'];
    expect(['light', 'dark']).toContain(themeAfter);
    expect(themeAfter).not.toBe(themeBefore);
    expect(toggle().getAttribute('aria-label')).not.toBe(labelBefore);
  });
});
