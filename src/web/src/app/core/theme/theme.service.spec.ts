import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset['theme'];
    TestBed.resetTestingModule();
  });

  it('uses a saved theme over the system preference', () => {
    localStorage.setItem('fm-theme', 'light');

    const service = TestBed.inject(ThemeService);
    TestBed.tick();

    expect(service.theme()).toBe('light');
    expect(document.documentElement.dataset['theme']).toBe('light');
  });

  it('toggles, applies the theme to <html> and remembers the choice', () => {
    localStorage.setItem('fm-theme', 'dark');
    const service = TestBed.inject(ThemeService);

    service.toggle();
    TestBed.tick();

    expect(service.theme()).toBe('light');
    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(localStorage.getItem('fm-theme')).toBe('light');
  });

  it('ignores an invalid saved value', () => {
    localStorage.setItem('fm-theme', 'purple');

    const service = TestBed.inject(ThemeService);

    expect(['light', 'dark']).toContain(service.theme());
  });
});
