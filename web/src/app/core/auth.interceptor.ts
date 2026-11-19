import { HttpInterceptorFn } from '@angular/common/http';
import { storageKeys } from '../../environments/environment';

/**
 * Подставляет Bearer-токен во все запросы к API.
 *
 * Токен читается из localStorage, а не из AuthService: interceptor вызывает
 * HttpClient, а AuthService сам зависит от HttpClient - так цикл зависимостей
 * не возникает.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = localStorage.getItem(storageKeys.token);

  if (token === null) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
