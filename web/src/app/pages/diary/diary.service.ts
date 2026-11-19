import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DiaryDay, Plan } from '../../core/api.models';

/** Запросы экрана дня: дневник за дату и цель на сегодня. */
@Injectable({ providedIn: 'root' })
export class DiaryService {
  private readonly http = inject(HttpClient);

  day(date: string): Observable<DiaryDay> {
    return this.http.get<DiaryDay>(`${environment.apiUrl}/diary`, { params: { date } });
  }

  plan(): Observable<Plan> {
    return this.http.get<Plan>(`${environment.apiUrl}/plan`);
  }
}
