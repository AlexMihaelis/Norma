import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddDiaryEntryRequest,
  CreateFoodRequest,
  DiaryDay,
  DiaryEntry,
  FoodSearchResponse,
  FoodSummary,
  Plan,
} from '../../core/api.models';

/** Запросы экрана дня: дневник, цель, поиск продукта и запись позиции. */
@Injectable({ providedIn: 'root' })
export class DiaryService {
  private readonly http = inject(HttpClient);

  day(date: string): Observable<DiaryDay> {
    return this.http.get<DiaryDay>(`${environment.apiUrl}/diary`, { params: { date } });
  }

  plan(): Observable<Plan> {
    return this.http.get<Plan>(`${environment.apiUrl}/plan`);
  }

  search(query: string): Observable<FoodSearchResponse> {
    return this.http.get<FoodSearchResponse>(`${environment.apiUrl}/foods/search`, {
      params: { q: query },
    });
  }

  add(request: AddDiaryEntryRequest): Observable<DiaryEntry> {
    return this.http.post<DiaryEntry>(`${environment.apiUrl}/diary`, request);
  }

  /** Свой продукт: попадает в общий каталог и сразу доступен поиску. */
  createFood(request: CreateFoodRequest): Observable<FoodSummary> {
    return this.http.post<FoodSummary>(`${environment.apiUrl}/foods`, request);
  }

  remove(entryId: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/diary/${entryId}`);
  }
}
