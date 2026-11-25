import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Plan, Stats } from '../../core/api.models';

/** Статистика за период: сервер отдает только дни с записями. */
@Injectable({ providedIn: 'root' })
export class StatsService {
  private readonly http = inject(HttpClient);

  get(from: string, to: string): Observable<Stats> {
    return this.http.get<Stats>(`${environment.apiUrl}/stats`, { params: { from, to } });
  }

  /** План текущего профиля: из него берем цели по БЖУ для строк «из N г». */
  plan(): Observable<Plan> {
    return this.http.get<Plan>(`${environment.apiUrl}/plan`);
  }
}
