import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { legacyLoginUrl } from './session';

/** A 401 from the facade means the legacy session expired: sign in again on the legacy page. */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        globalThis.location?.assign(legacyLoginUrl());
      }
      return throwError(() => error);
    }),
  );
