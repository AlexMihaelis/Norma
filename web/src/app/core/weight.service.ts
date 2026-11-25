import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BodyMeasurement,
  BodyMeasurementKind,
  BodyMeasurementLatest,
  WeightEntry,
  WeightList,
  WeightTrend,
} from './api.models';

/** Вес и обхваты тела: замеры, тренд, последние обхваты. */
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

  latestBodyMeasurements(): Observable<BodyMeasurementLatest[]> {
    return this.http.get<BodyMeasurementLatest[]>(`${environment.apiUrl}/body-measurements/latest`);
  }

  addBodyMeasurement(kind: BodyMeasurementKind, cm: number, date: string): Observable<BodyMeasurement> {
    return this.http.post<BodyMeasurement>(`${environment.apiUrl}/body-measurements`, { kind, cm, date });
  }
}
