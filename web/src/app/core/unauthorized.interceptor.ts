import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { loginUrl } from './session';

/**
 * A 401 from the facade means the legacy session expired: sign in again. Except on /session itself, where a 401 is
 * the answer to wrong credentials and the sign-in screen shows it.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url !== '/session'
      ) {
        globalThis.location?.assign(loginUrl());
      }
      return throwError(() => error);
    }),
  );
