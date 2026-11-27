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

  /**
   * Что показывать в шапке и приветствии: ник, иначе часть email до «@» с
   * большой буквы - полный адрес в шапке читается плохо.
   */
  readonly displayName = computed(() => {
    const nickname = this.nickname()?.trim();

    if (nickname) {
      return nickname;
    }

    const local = this.email()?.split('@')[0] ?? '';

    return local === '' ? '' : local.charAt(0).toUpperCase() + local.slice(1);
  });

  /**
   * Инициалы для кружка, если картинки нет.
   *
   * Ник - первое, что видит пользователь, поэтому буквы берутся из него:
   * «Анна Петрова» -> «АП», «Миха» -> «М». Если ника нет, работает та же
   * логика на части email до «@». Пустая строка вместо «?»: вопросительный знак
   * в аватаре выглядит как ошибка загрузки.
   */
  readonly initials = computed(() => {
    const nickname = this.nickname()?.trim();

    if (nickname) {
      return initialsOf(nickname.split(/\s+/));
    }

    const local = this.email()?.split('@')[0]?.trim();

    return local ? initialsOf(local.split(/[\s._-]+/)) : '';
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

/** Первые буквы первых двух слов: «Анна Петрова» -> «АП», «Миха» -> «М». */
function initialsOf(words: string[]): string {
  return words
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word.charAt(0).toUpperCase())
    .join('');
}
