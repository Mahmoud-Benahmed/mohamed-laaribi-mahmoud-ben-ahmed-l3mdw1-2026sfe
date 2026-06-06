import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { catchError, throwError } from "rxjs";
import { HttpError, isHttpError } from "../interfaces/HttpError";
import { TranslateService } from "@ngx-translate/core";
import { inject } from "@angular/core";

export const errorTranslateInterceptor: HttpInterceptorFn = (req, next) => {
  const translate = inject(TranslateService);

  const translateCode = (code: string): string | null => {
    const namespaces = [
      `auth.responses.errors.${code}`,
      `tenant.responses.errors.${code}`,
    ];
    for (const key of namespaces) {
      const translated = translate.instant(key);
      if (translated !== key) return translated;
    }
    return null;
  };

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {

      // ── Structured backend error ───────────────────────────────────────
      if (isHttpError(error.error)) {
        const code = error.error.code;
        const message = translateCode(code) ?? error.error.message ?? code;

        return throwError(() => new HttpErrorResponse({
          error: { ...error.error, message } as HttpError,
          headers: error.headers,
          status: error.status,
          statusText: error.statusText,
          url: error.url ?? undefined,
        }));
      }

      // ── Unstructured / network error ───────────────────────────────────
      let message: string;
      if (error.status === 0) {
        message = translate.instant('auth.responses.errors.SERVER_UNREACHABLE');
      } else if (error.status === 429) {
        message = translate.instant('auth.responses.errors.RATE_LIMIT');
      } else if (error.status >= 500) {
        message = translate.instant('auth.responses.errors.INTERNAL_ERROR');
      } else {
        message = translate.instant('auth.responses.errors.AUTH_000');
      }

      return throwError(() => new HttpErrorResponse({
        error: { message, statusCode: error.status, code: 'UNKNOWN' },
        headers: error.headers,
        status: error.status,
        statusText: error.statusText,
        url: error.url ?? undefined,
      }));
    })
  );
};