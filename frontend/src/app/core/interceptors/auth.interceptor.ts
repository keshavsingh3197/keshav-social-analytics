import { HttpContext, HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

const AUTH_RETRY_ATTEMPTED = new HttpContextToken<boolean>(() => false);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();
  const isOwnApi = req.url.startsWith(environment.apiUrl) || !req.url.startsWith('http');

  // Only attach bearer token to our own API requests so external public config calls
  // (e.g., GET id.keshavsingh.in/api/config) avoid unnecessary CORS preflights.
  if (token && isOwnApi) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(req).pipe(
    catchError((err: unknown) => {
      if (
        isOwnApi &&
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        !req.context.get(AUTH_RETRY_ATTEMPTED)
      ) {
        return auth.refresh().pipe(
          switchMap((session) => {
            if (!session?.accessToken) {
              return throwError(() => err);
            }
            const retriedReq = req.clone({
              context: new HttpContext().set(AUTH_RETRY_ATTEMPTED, true),
              setHeaders: {
                Authorization: `Bearer ${session.accessToken}`
              }
            });
            return next(retriedReq);
          }),
          catchError((retryOrRefreshErr: unknown) => {
            if (retryOrRefreshErr instanceof HttpErrorResponse && retryOrRefreshErr.status === 401) {
              return throwError(() => retryOrRefreshErr);
            }
            return throwError(() => err);
          })
        );
      }
      return throwError(() => err);
    })
  );
};
