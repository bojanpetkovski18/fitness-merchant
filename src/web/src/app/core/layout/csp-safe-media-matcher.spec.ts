import { BreakpointObserver, MediaMatcher } from '@angular/cdk/layout';
import { Platform } from '@angular/cdk/platform';
import { TestBed } from '@angular/core/testing';
import { CspSafeMediaMatcher } from './csp-safe-media-matcher';

describe('CspSafeMediaMatcher', () => {
  // Pretend to be Chrome: that is when the CDK adds its empty <style> workaround.
  const blink = { isBrowser: true, BLINK: true, WEBKIT: false } as Platform;

  beforeEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: (query: string) => ({
        matches: false,
        media: query,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
      }),
    });
    document.head.querySelectorAll('style').forEach((style) => style.remove());
  });

  function stylesAfterObserving(matcher: 'cdk' | 'csp-safe'): number {
    TestBed.configureTestingModule({
      providers: [
        { provide: Platform, useValue: blink },
        ...(matcher === 'csp-safe'
          ? [{ provide: MediaMatcher, useClass: CspSafeMediaMatcher }]
          : []),
      ],
    });
    TestBed.inject(BreakpointObserver).observe('(forced-colors: active)').subscribe();
    return document.querySelectorAll('style').length;
  }

  it('documents the problem: the CDK matcher inserts an inline <style> in Blink', () => {
    expect(stylesAfterObserving('cdk')).toBe(1);
  });

  it('inserts no <style>, so the strict CSP has nothing to block (K1)', () => {
    expect(stylesAfterObserving('csp-safe')).toBe(0);
  });
});
