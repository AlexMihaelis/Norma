import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateFoodRequest,
  FoodSearchResponse,
  FoodSummary,
  MealKind,
  Recipe,
  RecipeListResponse,
  RecipeRequest,
  RecipeTagListResponse,
} from '../../core/api.models';

export interface RecipeFilter {
  q?: string;
  tag?: string;
  autoTag?: string;
  favorite?: boolean;
}

/** Запросы экрана рецептов: список с фильтрами, карточка, метки и запись в дневник. */
@Injectable({ providedIn: 'root' })
export class RecipesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/recipes`;

  list(filter: RecipeFilter = {}): Observable<RecipeListResponse> {
    let params = new HttpParams();

    if (filter.q) {
      params = params.set('q', filter.q);
    }

    if (filter.tag) {
      params = params.set('tag', filter.tag);
    }

    if (filter.autoTag) {
      params = params.set('autoTag', filter.autoTag);
    }

    if (filter.favorite) {
      params = params.set('favorite', 'true');
    }

    return this.http.get<RecipeListResponse>(this.base, { params });
  }

  tags(): Observable<RecipeTagListResponse> {
    return this.http.get<RecipeTagListResponse>(`${this.base}/tags`);
  }

  get(id: string): Observable<Recipe> {
    return this.http.get<Recipe>(`${this.base}/${id}`);
  }

  create(request: RecipeRequest): Observable<Recipe> {
    return this.http.post<Recipe>(this.base, request);
  }

  update(id: string, request: RecipeRequest): Observable<Recipe> {
    return this.http.put<Recipe>(`${this.base}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  setFavorite(id: string, isFavorite: boolean): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/favorite`, { isFavorite });
  }

  /** Поиск продуктов для строк состава: тот же каталог, что и в дневнике. */
  searchFoods(query: string): Observable<FoodSearchResponse> {
    return this.http.get<FoodSearchResponse>(`${environment.apiUrl}/foods/search`, {
      params: new HttpParams().set('q', query),
    });
  }

  /** Быстрое создание продукта прямо из формы рецепта. */
  createFood(request: CreateFoodRequest): Observable<FoodSummary> {
    return this.http.post<FoodSummary>(`${environment.apiUrl}/foods`, request);
  }

  /** Запись рецепта в дневник: граммовка и прием пищи. */
  addToDiary(recipeId: string, quantity: number, meal: MealKind, date: string): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/diary`, { meal, recipeId, quantity, date });
  }
}

