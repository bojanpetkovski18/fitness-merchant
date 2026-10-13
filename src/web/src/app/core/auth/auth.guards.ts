import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session.store';

/** Signed-in users only; guests go to sign-in and come back afterwards. */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore);
  return (
    session.isSignedIn() ||
    inject(Router).createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } })
  );
};

/** Admins only. The API enforces this too; the guard just avoids showing screens that would fail. */
export const adminGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  if (!session.isSignedIn()) {
    return router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
  }
  return session.isAdmin() || router.createUrlTree(['/']);
};

/** Sign-in and register pages make no sense when already signed in. */
export const guestGuard: CanActivateFn = () =>
  !inject(SessionStore).isSignedIn() || inject(Router).createUrlTree(['/']);
