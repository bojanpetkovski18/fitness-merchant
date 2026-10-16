import {
  provideHttpClient,
  withFetch,
  withInterceptors,
  withXsrfConfiguration,
} from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  TitleStrategy,
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
} from '@angular/router';
import { MediaMatcher } from '@angular/cdk/layout';
import { routes } from './app.routes';
import { CspSafeMediaMatcher } from './core/layout/csp-safe-media-matcher';
import { AppTitleStrategy } from './core/app-title-strategy';
import { authInterceptor } from './core/auth/auth.interceptor';
import { SessionStore } from './core/auth/session.store';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // K1: no empty <style> elements from the CDK, which the strict CSP would block.
    { provide: MediaMatcher, useClass: CspSafeMediaMatcher },
    provideHttpClient(
      withFetch(),
      // The API issues the XSRF-TOKEN cookie; Angular echoes it on unsafe same-origin requests (D005).
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
      withInterceptors([authInterceptor]),
    ),
    // Know who is signed in before the first route (and its guards) runs.
    provideAppInitializer(() => inject(SessionStore).load()),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }),
    ),
    { provide: TitleStrategy, useClass: AppTitleStrategy },
  ],
};
