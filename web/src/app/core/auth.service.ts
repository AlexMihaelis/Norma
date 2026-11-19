import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment, storageKeys } from '../../environments/environment';
import { AuthResponse } from './api.models';

/**
 * Вход, регистрация и хранение токена. Токен лежит в localStorage: для dev это
 * удобно, но XSS его прочитает - в проде нужен httpOnly cookie или память.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly token = signal<string | null>(localStorage.getItem(storageKeys.token));
  readonly email = signal<string | null>(localStorage.getItem(storageKeys.email));
  readonly signedIn = computed(() => this.token() !== null);

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  register(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  logout(): void {
    localStorage.removeItem(storageKeys.token);
    localStorage.removeItem(storageKeys.email);
    this.token.set(null);
    this.email.set(null);
  }

  private store(response: AuthResponse): void {
    localStorage.setItem(storageKeys.token, response.token);
    localStorage.setItem(storageKeys.email, response.email);
    this.token.set(response.token);
    this.email.set(response.email);
  }
}
