import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const guestGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureAuthenticated().pipe(
    map(() => {
      const returnUrl = route.queryParamMap.get('returnUrl') ?? '/screenings';
      void router.navigateByUrl(returnUrl);
      return false;
    }),
    catchError(() => of(true))
  );
};
