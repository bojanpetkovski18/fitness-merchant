import { DOCUMENT, Injectable, effect, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const storageKey = 'fm-theme';

/**
 * Light/dark theme (D014). Starts from the saved choice, else the system preference, else dark.
 * Storage access is wrapped because it throws in some privacy modes; the theme still works, it is
 * just not remembered.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly theme = signal<Theme>(this.initialTheme());

  constructor() {
    effect(() => {
      this.document.documentElement.dataset['theme'] = this.theme();
    });
  }

  toggle(): void {
    const next: Theme = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    try {
      this.document.defaultView?.localStorage.setItem(storageKey, next);
    } catch {
      // Not persisted; acceptable.
    }
  }

  private initialTheme(): Theme {
    const view = this.document.defaultView;
    try {
      const saved = view?.localStorage.getItem(storageKey);
      if (saved === 'light' || saved === 'dark') {
        return saved;
      }
    } catch {
      // Fall through to the system preference.
    }
    return view?.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  }
}
