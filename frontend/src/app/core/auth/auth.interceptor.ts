import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

const AUTH_ENDPOINTS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

const isApi = (req: HttpRequest<unknown>) => req.url.startsWith(environment.apiUrl);
const isAuthEndpoint = (req: HttpRequest<unknown>) => AUTH_ENDPOINTS.some((p) => req.url.endsWith(p));

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/**
 * Adds the access token to API calls. When the API answers 401 (token expired), refreshes
 * once and replays the request; if the refresh fails the session is cleared.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApi(req) || isAuthEndpoint(req)) return next(req);

  const auth = inject(AuthService);

  return next(withToken(req, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) return throwError(() => error);

      return auth.refresh().pipe(
        switchMap((token) => (token ? next(withToken(req, token)) : throwError(() => error))),
      );
    }),
  );
};
