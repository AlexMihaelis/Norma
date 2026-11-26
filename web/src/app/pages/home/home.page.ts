import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, Observable, of } from 'rxjs';
import {
  ActivityDay,
  DiaryDay,
  DiaryTotals,
  Plan,
  WaterDay,
  WeightEntry,
  WeightTrend,
  activityKindTitles,
} from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { DiaryService } from '../diary/diary.service';
import { WeightService } from '../../core/weight.service';
import { AccountService } from '../../core/account.service';
import { HomeService } from './home.service';
import { NutritionRingComponent } from '../../ui/nutrition-ring/nutrition-ring.component';

interface SparklinePoint {
  id: string;
  x: number;
  y: number;
  kg: number;
  date: string;
}

interface Sparkline {
  polyline: string;
  area: string;
  points: SparklinePoint[];
}

interface WeightDelta {
  value: number;
  label: string;
}

/**
 * Главный экран: приветствие, вес с мини-графиком, питание за день, вода и
 * активность. Все запросы независимы: если один раздел недоступен (нет профиля,
 * нет веса), остальные карточки все равно рисуются.
 */
@Component({
  selector: 'app-home-page',
  imports: [RouterLink, NutritionRingComponent],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage implements OnInit {
  private readonly home = inject(HomeService);
  private readonly diaryService = inject(DiaryService);
  private readonly weights = inject(WeightService);
  protected readonly account = inject(AccountService);

  protected readonly error = signal<string | null>(null);
  protected readonly diary = signal<DiaryDay | null>(null);
  protected readonly plan = signal<Plan | null>(null);
  protected readonly weightList = signal<WeightEntry[]>([]);
  protected readonly trend = signal<WeightTrend | null>(null);
  protected readonly water = signal<WaterDay | null>(null);
  protected readonly activity = signal<ActivityDay | null>(null);
  protected readonly waterBusy = signal(false);

  protected readonly today = new Date();
  protected readonly dateIso = this.today.toISOString().slice(0, 10);
  protected readonly dateLabel = this.formatDateLabel(this.today);
  protected readonly activityTitles = activityKindTitles;

  protected readonly currentKg = computed(
    () => this.weightList()[0]?.kg ?? this.trend()?.currentEmaKg ?? null,
  );

  protected readonly goalKg = computed(() => this.trend()?.targetKg ?? null);

  protected readonly rate = computed(() => this.trend()?.rateKgPerWeek ?? null);

  protected readonly eaten = computed<DiaryTotals>(
    () => this.diary()?.totals ?? { kcal: 0, protein: 0, fat: 0, carbs: 0 },
  );

  protected readonly remainingKcal = computed(() => {
    const target = this.plan()?.targetKcal;

    return target === null || target === undefined ? null : Math.round(target - this.eaten().kcal);
  });

  protected readonly remainingMacros = computed(() => {
    const macros = this.plan()?.macros;

    if (!macros) {
      return null;
    }

    return {
      protein: Math.max(0, Math.round(macros.proteinG - this.eaten().protein)),
      fat: Math.max(0, Math.round(macros.fatG - this.eaten().fat)),
      carbs: Math.max(0, Math.round(macros.carbsG - this.eaten().carbs)),
    };
  });

  /** Мини-график веса: все замеры окна, подъемы и спады как есть. */
  protected readonly sparkline = computed<Sparkline>(() => {
    const entries = [...this.weightList()]
      .sort((left, right) => left.date.localeCompare(right.date))
      .slice(-14);

    if (entries.length === 0) {
      return { polyline: '', area: '', points: [] };
    }

    const values = entries.map((entry) => entry.kg);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;

    const points: SparklinePoint[] = entries.map((entry, index) => ({
      id: entry.id,
      x: entries.length === 1 ? 198 : (index / (entries.length - 1)) * 396,
      y: 6 + (1 - (entry.kg - min) / span) * 136,
      kg: entry.kg,
      date: entry.date,
    }));

    const polyline = points.map((point) => `${point.x.toFixed(1)},${point.y.toFixed(1)}`).join(' ');
    const first = points[0];
    const last = points[points.length - 1];
    const area = `M ${first.x.toFixed(1)},148 L ${polyline.replaceAll(' ', ' L ')} L ${last.x.toFixed(1)},148 Z`;

    return { polyline, area, points };
  });

  /**
   * Изменение веса: скорость из тренда, если история уже набралась, иначе
   * разница первого и последнего замера в окне - цифра под весом должна быть
   * видна всегда.
   */
  protected readonly weightDelta = computed<WeightDelta | null>(() => {
    const rate = this.trend()?.rateKgPerWeek;

    if (rate !== null && rate !== undefined) {
      return { value: rate, label: 'за неделю' };
    }

    const entries = [...this.weightList()].sort((left, right) => left.date.localeCompare(right.date));

    if (entries.length < 2) {
      return null;
    }

    const first = entries[0];
    const last = entries[entries.length - 1];
    const days = Math.max(
      1,
      Math.round(
        (new Date(last.date).getTime() - new Date(first.date).getTime()) / (24 * 60 * 60 * 1000),
      ),
    );

    return { value: last.kg - first.kg, label: `за ${days} дн.` };
  });

  protected readonly waterPercent = computed(() => {
    const water = this.water();
    const norm = water?.normMl;

    if (!water || !norm || norm <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((water.drunkMl / norm) * 100));
  });

  protected readonly firstActivity = computed(() => this.activity()?.items[0] ?? null);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.error.set(null);

    const fallback = <T>(source: Observable<T>) =>
      source.pipe(catchError(() => of(null)));

    forkJoin({
      diary: fallback(this.diaryService.day(this.dateIso)),
      plan: fallback(this.home.plan()),
      weights: fallback(this.weights.list()),
      trend: fallback(this.weights.trend()),
      water: fallback(this.home.water(this.dateIso)),
      activity: fallback(this.home.activity(this.dateIso)),
    }).subscribe({
      next: ({ diary, plan, weights, trend, water, activity }) => {
        this.diary.set(diary);
        this.plan.set(plan);
        this.weightList.set(weights?.items ?? []);
        this.trend.set(trend);
        this.water.set(water);
        this.activity.set(activity);
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected addWaterGlass(): void {
    if (this.waterBusy()) {
      return;
    }

    this.waterBusy.set(true);

    this.home.addWater(250, this.dateIso).subscribe({
      next: () => {
        this.waterBusy.set(false);
        this.home.water(this.dateIso).subscribe({
          next: (water) => this.water.set(water),
          error: () => this.waterBusy.set(false),
        });
      },
      error: (error: HttpErrorResponse) => {
        this.waterBusy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected formatKg(value: number | null): string {
    return value === null ? '—' : value.toFixed(1).replace('.', ',');
  }

  protected formatDelta(value: number, label: string): string {
    const sign = value > 0 ? '+' : '';

    return `${sign}${value.toFixed(1).replace('.', ',')} кг ${label}`;
  }

  protected formatNumber(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : value.toLocaleString('ru-RU');
  }

  protected abs(value: number): number {
    return Math.abs(value);
  }

  /** В макете легенда показывает «съедено / цель», например «60 / 140 г». */
  protected legendValue(consumed: number, target: number | undefined): string {
    const left = Math.round(consumed);

    return target === undefined ? `${left} г` : `${left} / ${Math.round(target)} г`;
  }

  private formatDateLabel(date: Date): string {
    const weekday = date.toLocaleDateString('ru-RU', { weekday: 'long' });
    const rest = date.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });

    return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${rest}`;
  }
}
