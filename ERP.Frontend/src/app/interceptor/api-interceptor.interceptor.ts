import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../environment';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const reqPrefix= environment.production ? `${environment.apiUrl}` : 'http://erp.local';
  if (!req.url.startsWith('http') && !req.url.startsWith('assets')) {
    const apiReq = req.clone({
      url: `${reqPrefix}${req.url}`
    });
    return next(apiReq);
  }

  return next(req);
};