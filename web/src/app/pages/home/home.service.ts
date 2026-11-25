import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ActivityDay, Plan, WaterDay } from '../../core/api.models';

/** Данные главного экрана, которых нет в других сервисах: план, вода, активность. */
@Injectable({ providedIn: 'root' })
export class HomeService {
  private readonly http = inject(HttpClient);

  plan(): Observable<Plan> {
    return this.http.get<Plan>(`${environment.apiUrl}/plan`);
  }

  water(date: string): Observable<WaterDay> {
    return this.http.get<WaterDay>(`${environment.apiUrl}/water`, { params: { date } });
  }

  activity(date: string): Observable<ActivityDay> {
    return this.http.get<ActivityDay>(`${environment.apiUrl}/activity`, { params: { date } });
  }

  /** Стакан воды: 250 мл, чтобы кнопка «+» на карточке воды работала без формы. */
  addWater(ml: number, date: string): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/water`, { ml, date });
  }
}
