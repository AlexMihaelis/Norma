import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { WeightEntry, WeightList, WeightTrend } from '../../core/api.models';

/** Вес: список замеров, запись нового и тренд с ИМТ. */
@Injectable({ providedIn: 'root' })
export class WeightService {
  private readonly http = inject(HttpClient);

  list(): Observable<WeightList> {
    return this.http.get<WeightList>(`${environment.apiUrl}/weight`);
  }

  trend(): Observable<WeightTrend> {
    return this.http.get<WeightTrend>(`${environment.apiUrl}/weight/trend`);
  }

  add(kg: number, date: string): Observable<WeightEntry> {
    return this.http.post<WeightEntry>(`${environment.apiUrl}/weight`, { kg, date });
  }

  /** Исправить существующий замер: опечатку в весе или дату. */
  update(id: string, kg: number, date: string): Observable<WeightEntry> {
    return this.http.put<WeightEntry>(`${environment.apiUrl}/weight/${id}`, { kg, date });
  }
}
