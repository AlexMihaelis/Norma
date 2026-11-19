import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Profile, UpdateProfileRequest } from '../../core/api.models';

/** Профиль: 404 значит «еще не заполнен», это нормальное состояние, а не ошибка. */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);

  get(): Observable<Profile | null> {
    return this.http.get<Profile>(`${environment.apiUrl}/me/profile`).pipe(
      catchError((error: HttpErrorResponse) =>
        error.status === 404 ? of(null) : throwError(() => error)),
    );
  }

  save(request: UpdateProfileRequest): Observable<Profile> {
    return this.http.put<Profile>(`${environment.apiUrl}/me/profile`, request);
  }
}
