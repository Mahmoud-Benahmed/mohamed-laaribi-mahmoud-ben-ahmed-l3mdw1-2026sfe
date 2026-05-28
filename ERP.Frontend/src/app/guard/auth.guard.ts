import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService, PRIVILEGES } from '../services/auth/auth.service';
import { environment } from '../environment';
import { map, catchError, of } from 'rxjs';

export const authGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  const path   = route.routeConfig?.path ?? '';

  const proceedWithChecks = () => {
    // ── Force password change
    if (auth.getMustChangePassword() && path !== 'must-change-password' && environment.production) {
      return router.createUrlTree(['/must-change-password']);
    }

    // ── Privilege-based access
    const requiredPrivileges = route.data['privileges'] as string[] | undefined;

    if (requiredPrivileges?.length) {
      const hasAccess = requiredPrivileges.some(p => auth.hasPrivilege(p));

      if (!hasAccess) {
        if (auth.hasPrivilege(PRIVILEGES.USERS.VIEW_USERS))       return router.createUrlTree(['/users']);
        if (auth.hasPrivilege(PRIVILEGES.ARTICLES.VIEW_ARTICLES)) return router.createUrlTree(['/articles']);
        if (auth.hasPrivilege(PRIVILEGES.CLIENTS.VIEW_CLIENTS))   return router.createUrlTree(['/clients']);
        if (auth.hasPrivilege(PRIVILEGES.STOCK.VIEW_STOCK))       return router.createUrlTree(['/stock']);
        return router.createUrlTree(['/home']);
      }
    }

    return true;
  };

  // ── Token still valid client-side — proceed immediately
  if (auth.isLoggedIn()) {
    return proceedWithChecks();
  }

  // ── No refresh token — send to plans (public landing page)
  const refreshToken = auth.getRefreshToken();
  if (!refreshToken) {
    return router.createUrlTree(['/plans']); // ← was '/login', now goes to plans
  }

  // ── Token expired — attempt silent refresh
  return auth.refresh({ refreshToken }).pipe(
    map(() => proceedWithChecks()),
    catchError(() => {
      auth.logout();
      return of(router.createUrlTree(['/plans'])); // ← was '/login', now goes to plans
    })
  );
};