import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot } from '@angular/router';
import { Location } from '@angular/common'; // ← add this
import { AuthService, PRIVILEGES } from '../services/auth/auth.service';
import { environment } from '../environment';
import { map, catchError, of } from 'rxjs';

export const authGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth     = inject(AuthService);
  const router   = inject(Router);
  const location = inject(Location);
  const path     = route.routeConfig?.path ?? '';

  const proceedWithChecks = () => {
    const mustChange = auth.getMustChangePassword();

    if (!mustChange && path === 'must-change-password') {
      return router.createUrlTree(['/home']);
    }
    if (mustChange && path !== 'must-change-password' && environment.production) {
      return router.createUrlTree(['/must-change-password']);
    }

    const requiredPrivileges = route.data['privileges'] as string[] | undefined;

    if (requiredPrivileges?.length) {
      const hasAccess = requiredPrivileges.some(p => auth.hasPrivilege(p));
      if (!hasAccess) {
        if (window.history.length > 1) {
          location.back();
          return false;
        }
        return router.createUrlTree(['/home']);
      }
    }

    return true;
  };

  if (auth.isLoggedIn()) return proceedWithChecks();

  const refreshToken = auth.getRefreshToken();
  if (!refreshToken) return router.createUrlTree(['/plans']);

  return auth.refresh({ refreshToken }).pipe(
    map(() => proceedWithChecks()),
    catchError(() => { auth.logout(); return of(router.createUrlTree(['/plans'])); })
  );
};