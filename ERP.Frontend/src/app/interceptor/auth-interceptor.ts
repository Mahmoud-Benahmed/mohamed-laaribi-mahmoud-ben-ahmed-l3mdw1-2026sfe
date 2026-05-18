import { inject } from "@angular/core";
import { catchError, finalize, Observable, shareReplay, switchMap, throwError } from "rxjs";
import { AuthService } from "../services/auth/auth.service";
import { Router } from "@angular/router";
import { MatDialog } from "@angular/material/dialog";
import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { ModalComponent } from "../components/modal/modal";
import { AuthResponseDto } from "../interfaces/AuthDto";

let serverDownDialogOpen = false;
let refreshInProgress$: Observable<AuthResponseDto> | null = null;

export const AuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.headers.has('X-Retry')) {
    return next(req.clone({ headers: req.headers.delete('X-Retry') }));
  }

  const auth   = inject(AuthService);
  const dialog = inject(MatDialog);
  const router = inject(Router);

  // ── Never attach token or intercept errors for auth infrastructure calls
  const isPublicCall = req.url.includes('/auth/refresh')
                    || req.url.includes('/auth/revoke')
                    || req.url.includes('/auth/login');

  const token = !isPublicCall ? auth.getAccessToken() : null;

  const tenantSlug = auth.getTenantSlug();
  const headers: Record<string, string> = { Authorization: `Bearer ${token}` };
  if (tenantSlug) headers['X-Tenant'] = tenantSlug;

  const authReq = token
    ? req.clone({ setHeaders: headers })
    : req;

  if (isPublicCall) {
    return next(authReq); // ← bypass all error handling below
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {

      // Server unreachable 
      if (error.status === 0) {
        if (!serverDownDialogOpen) {
          serverDownDialogOpen = true;
          dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: 'Server Unreachable',
              message: 'Unable to connect to the server. Check your connection or try again later.',
              confirmText: 'OK',
              showCancel: false,
              icon: 'cloud_off',
              iconColor: 'warn'
            }
          }).afterClosed().subscribe(() => {
            serverDownDialogOpen = false;
          });
        }
        auth.logout();
        return throwError(() => error);
      }

      // Rate limit for login and create tenants 
      if (error.status === 429) {
        const retryAfter = error.headers.get('Retry-After');

        let content = error.error?.content;
        if (!content) {
          if (req.url.includes('/tenants')) {
            content = 'Too many registration attempts. Please wait 10 minutes before retrying.';
          } else if (req.url.includes('/auth/login')) {
            content = `Too many login attempts. Please wait ${retryAfter ?? 60} seconds before retrying.`;
          } else {
            content = `Too many requests. Please wait ${retryAfter ? retryAfter + ' seconds' : 'a few minutes'} before retrying.`;
          }
        }

        dialog.open(ModalComponent, {
          width: '400px',
          data: {
            title: 'Rate Limit Reached',
            message: content,
            confirmText: 'OK',
            showCancel: false,
            icon: 'timer',
            iconColor: 'warn'
          }
        }).afterClosed().subscribe(() => {
          router.navigate(['/home']);
        });
        return throwError(() => error);
      }

      // ── Forbidden ──────────────────────────────────────────────────────
      if (error.status === 403) {
        const code = error.error?.code;
        const isInactive = code === 'AUTH_003';

        dialog.open(ModalComponent, {
          width: '400px',
          data: {
            title: isInactive ? 'Account Deactivated' : 'Access Denied',
            message: error.error?.message ?? 'You do not have permission to perform this action.',
            confirmText: 'OK',
            showCancel: false,
            icon: isInactive ? 'person_off' : 'block',
            iconColor: 'danger'
          }
        }).afterClosed().subscribe(() => {
          if (isInactive) {
            auth.logout();
          } else {
            router.navigate(['/home']);
          }
        });
        return throwError(() => error);
      }

      // ── Unauthorized ───────────────────────────────────────────────────
      if (error.status === 401) {
        const code = error.error?.code;

        // User deleted or inactive — session invalid
        if (code === 'AUTH_009') {
          dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: 'Session Expired',
              message: 'Your session is no longer valid. You will be logged out.',
              confirmText: 'OK',
              showCancel: false,
              icon: 'person_off',
              iconColor: 'danger'
            }
          }).afterClosed().subscribe(() => auth.logout());
          return throwError(() => error);
        }

        // Wrong current password — user is authenticated, just typed wrong
        if (code === 'AUTH_002') {
          return throwError(() => error);
        }

        // Security violation
        if (code === 'AUTH_008') {
          auth.logout();
          return throwError(() => error);
        }

        // Token expired — attempt refresh
        const refreshToken = auth.getRefreshToken();
        if (!refreshToken) {
          auth.logout();
          return throwError(() => error);
        }

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
          catchError(refreshError => {
            auth.logout();
            return throwError(() => refreshError);
          })
        );
      }

      // ── Not found ──────────────────────────────────────────────────────
      if (error.status === 404) {
        const isCacheEndpoint = req.url.includes('/cache/');
        if (!isCacheEndpoint) {
          router.navigate(['/home']);
        }
        return throwError(() => error);
      }

      // ── Gateway / service unavailable ──────────────────────────────────
      if (error.status === 503 || error.status === 502 || error.status === 504) {
        if (!serverDownDialogOpen) {
          serverDownDialogOpen = true;
          dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: 'Service Unavailable',
              message: 'The requested service is temporarily unavailable. Please try again later.',
              confirmText: 'OK',
              showCancel: false,
              icon: 'cloud_off',
              iconColor: 'warn'
            }
          }).afterClosed().subscribe(() => {
            serverDownDialogOpen = false;
            router.navigate(['/home']);
          });
        }
        return throwError(() => error);
      }

      return throwError(() => error);
    })
  );
};