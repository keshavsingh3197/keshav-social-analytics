import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.ensureInitialized().pipe(
    map(() => {
      if (auth.isAuthenticated()) return true;
      auth.loginRedirect();
      return false;
    })
  );
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.ensureInitialized().pipe(
    map(() => {
      if (!auth.isAuthenticated()) {
        auth.loginRedirect();
        return false;
      }
      if (auth.isAdmin()) {
        return true;
      }
      return router.createUrlTree(['/dashboard'], { queryParams: { denied: 'admin' } });
    })
  );
};
