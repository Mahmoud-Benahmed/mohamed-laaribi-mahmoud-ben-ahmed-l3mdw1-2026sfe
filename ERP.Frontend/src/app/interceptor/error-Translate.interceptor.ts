import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { catchError, switchMap, of, from, throwError } from "rxjs";
import { HttpError, isHttpError } from "../interfaces/HttpError";
import { TranslateService } from "@ngx-translate/core";
import { inject } from "@angular/core";

// Helper: try namespaces asynchronously
async function findTranslation(namespaces: string[], translate: TranslateService, params?: object): Promise<string | null> {
  for (const key of namespaces) {
    const translated = await translate.get(key, params).toPromise();
    if (translated && translated !== key) return translated;
  }
  return null;
}

export const errorTranslateInterceptor: HttpInterceptorFn = (req, next) => {
  const translate = inject(TranslateService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Structured backend error
    if (isHttpError(error.error)) {
      const code = error.error.code;
      const interpolationParams = { key: error.error.message };

      const namespaces = [
        `auth.responses.errors.${code}`,
        `tenants.responses.errors.${code}`,
        `articles.responses.errors.${code}`,
        `clients.responses.errors.${code}`,
        `stock.responses.errors.${code}`,
        `invoices.responses.errors.${code}`,
        `payments.responses.errors.${code}`,
      ];

      return from(findTranslation(namespaces, translate, interpolationParams)).pipe(
        switchMap(translatedMessage => {
          const finalMessage = translatedMessage ?? error.error.message ?? code;
          return throwError(() => new HttpErrorResponse({
            error: { ...error.error, message: finalMessage } as HttpError,
            headers: error.headers,
            status: error.status,
            statusText: error.statusText,
            url: error.url ?? undefined,
          }));
        })
      );
    }

      // Unstructured / network error (remains synchronous, fine)
      let message: string;
      if (error.status === 0) {
        message = 'auth.responses.errors.SERVER_UNREACHABLE';
      } else if (error.status === 429) {
        message = 'auth.responses.errors.RATE_LIMIT';
      } else if (error.status >= 500) {
        message = 'auth.responses.errors.INTERNAL_ERROR';
      } else {
        message = 'auth.responses.errors.AUTH_000';
      }

      // Also translate generic errors asynchronously
      return from(translate.get(message).toPromise()).pipe(
        switchMap(translated => {
          const finalMessage = typeof translated === 'string' ? translated : message;
          return throwError(() => new HttpErrorResponse({
            error: { message: finalMessage, statusCode: error.status, code: 'UNKNOWN' },
            headers: error.headers,
            status: error.status,
            statusText: error.statusText,
            url: error.url ?? undefined,
          }));
        })
      );
    })
  );
};