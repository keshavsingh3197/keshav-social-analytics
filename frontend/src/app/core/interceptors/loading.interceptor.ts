import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';

export const SILENT_HTTP = new HttpContextToken<boolean>(() => false);

const DEFAULT_SILENT_PATTERNS = ['/health', '/stream'];

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  if (
    req.context.get(SILENT_HTTP) ||
    DEFAULT_SILENT_PATTERNS.some((p) => req.url.includes(p))
  ) {
    return next(req);
  }

  const loading = inject(LoadingService);
  loading.beginRequest();
  return next(req).pipe(finalize(() => loading.endRequest()));
};
