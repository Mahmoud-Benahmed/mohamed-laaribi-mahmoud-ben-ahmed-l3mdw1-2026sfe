import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, EMPTY, throwError } from 'rxjs';

export const TenantInactiveInterceptor: HttpInterceptorFn = (req, next) => {
  const router       = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 403 && error.error?.code === 'TENANT_INACTIVE') {
          router.navigate(['/subscription-expiry']);
          return EMPTY;
      }
      return throwError(() => error);
    })
  );
};