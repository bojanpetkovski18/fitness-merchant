import { MediaMatcher } from '@angular/cdk/layout';
import { DOCUMENT, Injectable, inject } from '@angular/core';

/**
 * The CDK MediaMatcher inserts an empty <style> element in Blink and WebKit (a workaround for old WebKit, where
 * matchMedia listeners only fired with a matching style rule). The strict CSP (style-src 'self') blocks it and
 * logs a violation on every page (K1). Current browsers do not need the workaround, so this calls matchMedia
 * directly. It serves every CDK user: BreakpointObserver and the high-contrast detector of the A11yModule.
 */
@Injectable()
export class CspSafeMediaMatcher extends MediaMatcher {
  private readonly view = inject(DOCUMENT).defaultView;

  override matchMedia(query: string): MediaQueryList {
    return this.view?.matchMedia ? this.view.matchMedia(query) : super.matchMedia(query);
  }
}
