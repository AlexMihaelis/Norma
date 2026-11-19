import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Пускает на экран только с токеном, иначе отправляет на вход. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.signedIn() ? true : router.createUrlTree(['/login']);
};
