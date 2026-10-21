import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { isDevMode } from '@angular/core';
import { catchError, throwError } from 'rxjs';

/** Logs unexpected API failures in development. Expected 4xx are handled by the caller. */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (isDevMode() && (error.status === 0 || error.status >= 500)) {
        console.error(`[API] ${req.method} ${req.url} failed`, error.error ?? error.message);
      }
      return throwError(() => error);
    }),
  );
