import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  BmiCategory,
  WeightEntry,
  WeightTrend,
  bmiCategoryTitles,
} from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { WeightChartComponent } from './weight-chart.component';
import { WeightService } from './weight.service';
import { ZoneScaleComponent } from './zone-scale.component';

/** Экран веса: запись замера, тренд, зоны веса и ИМТ, прогноз цели. */
@Component({
  selector: 'app-weight-page',
  imports: [ReactiveFormsModule, WeightChartComponent, ZoneScaleComponent],
  templateUrl: './weight.page.html',
  styleUrl: './weight.page.scss',
})
export class WeightPage implements OnInit {
  private readonly weights = inject(WeightService);

  protected readonly entries = signal<WeightEntry[]>([]);
  protected readonly trend = signal<WeightTrend | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly busy = signal(false);

  protected readonly bmiTitles = bmiCategoryTitles;
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly zoneColors = ['#60a5fa', '#22c55e', '#f59e0b', '#dc2626'];
  protected readonly weightZoneLabels = [
    'Недостаток веса',
    'Норма',
    'Избыток веса',
    'Ожирение',
  ] as const;
  protected readonly bmiZoneLabels = [
    'Низкий',
    'Нормальный',
    'Высокий',
    'Очень высокий',
  ] as const;

  protected readonly form = new FormGroup({
    kg: new FormControl<number | null>(null),
    date: new FormControl(this.today),
  });

  /** Текущий вес - самый свежий замер; по нему ставим маркер на шкале. */
  protected readonly currentKg = computed(() => this.entries()[0]?.kg ?? null);

  protected readonly weightBoundaries = computed<readonly [number, number, number] | null>(() => {
    const trend = this.trend();
    const normal = trend?.normalWeight;

    if (!trend || !normal) {
      return null;
    }

    return [normal.minKg, normal.maxKg, trend.obeseFromKg ?? normal.maxKg * 1.2];
  });

  protected readonly bmiBoundaries = computed<readonly [number, number, number] | null>(() => {
    const normal = this.trend()?.normalWeight;

    return normal === null || normal === undefined
      ? null
      : [normal.bmiMin, normal.bmiMax, 30];
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    forkJoin({
      list: this.weights.list(),
      trend: this.weights.trend(),
    }).subscribe({
      next: ({ list, trend }) => {
        this.entries.set(list.items);
        this.trend.set(trend);
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected submit(): void {
    const value = this.form.getRawValue();
    const kg = value.kg;

    if (kg === null || kg < 20 || kg > 500) {
      this.error.set('Вес должен быть в диапазоне 20-500 кг.');
      return;
    }

    this.busy.set(true);
    this.saved.set(false);
    this.error.set(null);

    this.weights.add(kg, value.date ?? this.today).subscribe({
      next: () => {
        this.busy.set(false);
        this.saved.set(true);
        this.form.controls.kg.setValue(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected format(value: number | null | undefined, digits = 1): string {
    if (value === null || value === undefined) {
      return '—';
    }

    return value.toFixed(digits).replace('.', ',');
  }

  protected formatDate(value: string | null): string {
    if (value === null) {
      return '—';
    }

    return new Date(value).toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }

  protected bmiClass(category: BmiCategory | null): string {
    return category === null ? 'neutral' : category.toLowerCase();
  }
}