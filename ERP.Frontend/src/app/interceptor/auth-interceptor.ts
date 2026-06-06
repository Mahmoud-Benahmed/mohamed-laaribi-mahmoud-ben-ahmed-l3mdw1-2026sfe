import { inject } from "@angular/core";
import { catchError, finalize, Observable, shareReplay, switchMap, throwError } from "rxjs";
import { AuthService } from "../services/auth/auth.service";
import { Router } from "@angular/router";
import { MatDialog } from "@angular/material/dialog";
import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { ModalComponent } from "../components/modal/modal";
import { AuthResponseDto } from "../interfaces/AuthDto";
import { TranslateService } from "@ngx-translate/core";

let serverDownDialogOpen = false;
let refreshInProgress$: Observable<AuthResponseDto> | null = null;

export const AuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.headers.has('X-Retry')) {
    return next(req.clone({ headers: req.headers.delete('X-Retry') }));
  }

  const auth      = inject(AuthService);
  const dialog    = inject(MatDialog);
  const router    = inject(Router);
  const translate = inject(TranslateService);

  const t = (key: string) => translate.instant(key);
  const e = (key: string) => translate.instant(`auth.responses.errors.${key}`);

  const isPublicCall = req.url.includes('/auth/refresh')
                    || req.url.includes('/auth/revoke')
                    || req.url.includes('/login');

  const token   = !isPublicCall ? auth.getAccessToken() : null;
  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  if (isPublicCall) return next(authReq);

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {

      // ── Server unreachable ─────────────────────────────────────────────
      if (error.status === 0) {
        if (!serverDownDialogOpen) {
          serverDownDialogOpen = true;
          dialog.open(ModalComponent, {
            width: '540px',
            data: {
              title:       e('SERVER_UNREACHABLE'),
              message:     e('SERVER_UNREACHABLE_MSG'),
              confirmText: t('common.confirm'),
              showCancel:  false,
              icon:        'cloud_off',
              iconColor:   'warn'
            }
          }).afterClosed().subscribe(() => { serverDownDialogOpen = false; });
        }
        auth.logout();
        return throwError(() => error);
      }

      // ── Rate limit ─────────────────────────────────────────────────────
      if (error.status === 429) {
        const retryAfter = error.headers.get('Retry-After');
        let message = error.error?.content;
        if (!message) {
          if (req.url.includes('/tenants')) {
            message = e('RATE_LIMIT_TENANTS');
          } else if (req.url.includes('/auth/login')) {
            message = translate.instant('auth.responses.errors.RATE_LIMIT_LOGIN', { seconds: retryAfter ?? 60 });
          } else {
            message = translate.instant('auth.responses.errors.RATE_LIMIT', { time: retryAfter ? retryAfter + 's' : translate.instant('auth.responses.errors.RATE_LIMIT_FALLBACK') });
          }
        }
        dialog.open(ModalComponent, {
          width: '540px',
          data: {
            title:       e('RATE_LIMIT'),
            message,
            confirmText: t('common.confirm'),
            showCancel:  false,
            icon:        'timer',
            iconColor:   'warn'
          }
        }).afterClosed().subscribe(() => router.navigate(['/home']));
        return throwError(() => error);
      }

      // ── Forbidden ──────────────────────────────────────────────────────
      if (error.status === 403) {
        const code = error.error?.code;
        if (code === 'TENANT_INACTIVE') return throwError(() => error);
        if (code === 'TENANT_USER_LIMIT_REACHED') return throwError(() => error);


        const isInactive = code === 'AUTH_003';
        dialog.open(ModalComponent, {
          width: '540px',
          data: {
            title:       isInactive ? e('AUTH_003_TITLE') : e('ACCESS_DENIED'),
            message:     error.error?.message ?? e('AUTH_006'),
            confirmText: t('common.confirm'),
            showCancel:  false,
            icon:        isInactive ? 'person_off' : 'block',
            iconColor:   'danger'
          }
        }).afterClosed().subscribe(() => {
          isInactive ? auth.logout() : router.navigate(['/home']);
        });
        return throwError(() => error);
      }

      // ── Unauthorized ───────────────────────────────────────────────────
      if (error.status === 401 || (error.status === 400 && error.error?.code === 'TENANT_001')) {
        const code = error.error?.code;

        if (code === 'AUTH_009') {
          dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title:       e('AUTH_019'),
              message:     e('AUTH_009'),
              confirmText: t('common.confirm'),
              showCancel:  false,
              icon:        'person_off',
              iconColor:   'danger'
            }
          }).afterClosed().subscribe(() => auth.logout());
          return throwError(() => error);
        }

        if (code === 'AUTH_002') return throwError(() => error);
        if (code === 'AUTH_008') { auth.logout(); return throwError(() => error); }

        const refreshToken = auth.getRefreshToken();
        if (!refreshToken) { auth.logout(); return throwError(() => error); }

        if (!refreshInProgress$) {
          refreshInProgress$ = auth.refresh({ refreshToken }).pipe(
            shareReplay(1),
            finalize(() => { refreshInProgress$ = null; })
          );
        }

        return refreshInProgress$.pipe(
          switchMap(response =>
            next(req.clone({
              setHeaders: { Authorization: `Bearer ${response.accessToken}` },
              headers: req.headers.set('X-Retry', 'true')
            }))
          ),
          catchError(refreshError => { auth.logout(); return throwError(() => refreshError); })
        );
      }

      // ── Not found ──────────────────────────────────────────────────────
      if (error.status === 404) {
        if (!req.url.includes('/cache/')) router.navigate(['/home']);
        return throwError(() => error);
      }

      // ── Gateway / service unavailable ──────────────────────────────────
      if (error.status === 503 || error.status === 502 || error.status === 504) {
        if (!serverDownDialogOpen) {
          serverDownDialogOpen = true;
          dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title:       e('SERVICE_UNAVAILABLE'),
              message:     e('SERVICE_UNAVAILABLE_MSG'),
              confirmText: t('common.confirm'),
              showCancel:  false,
              icon:        'cloud_off',
              iconColor:   'warn'
            }
          }).afterClosed().subscribe(() => { serverDownDialogOpen = false; router.navigate(['/home']); });
        }
        return throwError(() => error);
      }

      return throwError(() => error);
    })
  );
};