import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { loginUrl } from './session';

/** Answers where a 401 is an answer, not an expired session. */
const ANSWERED_HERE = new Set([
  '/session', // wrong credentials: the sign-in screen shows the message
  '/api/session', // nobody signed in: that is what Session.refresh() asks
]);

/**
 * A 401 from the facade means the legacy session expired: sign in again. Never from the sign-in screen itself, where
 * a redirect would reload the same page forever.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) => {
      const onSignIn = globalThis.location?.pathname?.startsWith('/app/connexion') ?? false;
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !ANSWERED_HERE.has(request.url) &&
        !onSignIn
      ) {
        globalThis.location?.assign(loginUrl());
      }
      return throwError(() => error);
    }),
  );
