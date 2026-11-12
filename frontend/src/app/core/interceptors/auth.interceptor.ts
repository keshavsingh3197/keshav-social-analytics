import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  // Only attach bearer token to our own API requests so external public config calls
  // (e.g., GET id.keshavsingh.in/api/config) avoid unnecessary CORS preflights.
  if (token && (req.url.startsWith(environment.apiUrl) || !req.url.startsWith('http'))) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(req);
};
