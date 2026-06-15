import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';

export const LoadingInterceptor: HttpInterceptorFn = (req, next) => {
  const loader = inject(LoadingService);

  const isI18n = req.url.includes('/assets/i18n/')
              || req.url.includes('/i18n/')
              || req.url.endsWith('.json');

  if (isI18n) return next(req);

  loader.show();
  return next(req).pipe(
    finalize(() => loader.hide())
  );
};
