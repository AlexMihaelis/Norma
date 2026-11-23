import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, of, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { Me } from './api.models';

/**
 * Данные аккаунта для шапки и профиля: ник, аватар, email.
 *
 * Держим их в сервисе-синглтоне, чтобы шапка и экран профиля обновлялись вместе:
 * сохранил ник - он сразу поменялся в шапке.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly email = signal<string | null>(this.auth.email());
  readonly nickname = signal<string | null>(null);
  readonly avatarDataUrl = signal<string | null>(null);

  /** Что показывать в шапке: ник, иначе email. */
  readonly displayName = computed(() => this.nickname() ?? this.email() ?? '');

  /** Инициалы для кружка, если картинки нет: «Анна Петрова» -> «АП». */
  readonly initials = computed(() => {
    const source = this.nickname()?.trim() || this.email() || '?';
    const parts = source.split(/[\s@._-]+/).filter(Boolean);

    return parts
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  /** Загружает аккаунт, если есть токен. Ошибку глотаем: guard разберется. */
  load(): Observable<Me | null> {
    if (!this.auth.signedIn()) {
      this.reset();

      return of(null);
    }

    return this.http.get<Me>(`${environment.apiUrl}/me`).pipe(
      tap((me) => this.apply(me)),
      catchError(() => {
        this.reset();

        return of(null);
      }),
    );
  }

  updateNickname(nickname: string): Observable<Me> {
    return this.http
      .put<Me>(`${environment.apiUrl}/me/nickname`, { nickname })
      .pipe(tap((me) => this.apply(me)));
  }

  uploadAvatar(dataUrl: string): Observable<Me> {
    return this.http
      .put<Me>(`${environment.apiUrl}/me/avatar`, { dataUrl })
      .pipe(tap((me) => this.apply(me)));
  }

  removeAvatar(): Observable<Me> {
    return this.http
      .delete<Me>(`${environment.apiUrl}/me/avatar`)
      .pipe(tap((me) => this.apply(me)));
  }

  reset(): void {
    this.email.set(null);
    this.nickname.set(null);
    this.avatarDataUrl.set(null);
  }

  private apply(me: Me): void {
    this.email.set(me.email);
    this.nickname.set(me.nickname);
    this.avatarDataUrl.set(me.avatarDataUrl);
  }
}