import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.ensureAuthenticated().pipe(map(valid => valid ? true : router.createUrlTree(['/login'])));
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.ensureAuthenticated().pipe(map(valid => valid ? router.createUrlTree(['/dashboard']) : true));
};

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const roles = route.data['roles'] as string[] | undefined;
  return auth.ensureAuthenticated().pipe(map(valid => !valid
    ? router.createUrlTree(['/login'])
    : !roles?.length || auth.hasRole(...roles) ? true : router.createUrlTree(['/dashboard'])));
};
