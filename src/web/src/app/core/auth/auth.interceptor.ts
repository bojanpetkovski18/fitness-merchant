import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SessionStore } from './session.store';

/**
 * A 401 from a protected API call means the session cookie expired or was revoked: forget the user so the UI
 * and guards reflect it. Auth endpoints are excluded (a failed sign-in is not a lost session).
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionStore);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !request.url.startsWith('/api/auth/')
      ) {
        session.clear();
      }
      return throwError(() => error);
    }),
  );
};
