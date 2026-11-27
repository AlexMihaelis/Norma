import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
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
import { NutritionRingComponent } from '../../ui/nutrition-ring/nutrition-ring.component';
import { HomeService } from './home.service';

interface ChartPoint {
  id: string;
  x: number;
  y: number;
  kg: number;
  measured: boolean;
}

interface ChartTick {
  y: number;
  label: string;
}

interface ChartLabel {
  x: number;
  text: string;
}

interface WeekChart {
  points: ChartPoint[];
  polyline: string;
  area: string;
  ticks: ChartTick[];
  labels: ChartLabel[];
  last: ChartPoint | null;
  bubble: { x: number; y: number } | null;
}

interface MacroRow {
  key: 'protein' | 'fat' | 'carbs';
  title: string;
  grams: number;
  percent: number;
}

interface WeightDelta {
  value: number;
  label: string;
}

/** Прямоугольник графика в координатах SVG. */
const PlotLeft = 40;
const PlotRight = 336;
const PlotTop = 26;
const PlotBottom = 168;

/**
 * Главный экран по макету: приветствие, график веса, последний вес, кольцо КБЖУ,
 * вода и активность. Все запросы независимы: нет веса или профиля - остальные
 * карточки все равно рисуются.
 */
@Component({
  selector: 'app-home-page',
  imports: [RouterLink, DecimalPipe, NutritionRingComponent],
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
  protected readonly activityTitles = activityKindTitles;

  /** Границы области графика: те же числа, что в шаблоне SVG. */
  protected readonly plot = { left: PlotLeft, right: PlotRight, top: PlotTop, bottom: PlotBottom };

  /** Приветствие по времени суток, как в макете. */
  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();

    return hour < 12 ? 'Доброе утро' : hour < 18 ? 'Добрый день' : 'Добрый вечер';
  });

  protected readonly currentKg = computed(
    () => this.weightList()[0]?.kg ?? this.trend()?.currentEmaKg ?? null,
  );

  protected readonly eaten = computed<DiaryTotals>(
    () => this.diary()?.totals ?? { kcal: 0, protein: 0, fat: 0, carbs: 0 },
  );

  /** Легенда кольца: граммы съеденного и доля калорий макроса. */
  protected readonly macros = computed<MacroRow[]>(() => {
    const eatenNow = this.eaten();
    const proteinKcal = eatenNow.protein * 4;
    const fatKcal = eatenNow.fat * 9;
    const carbsKcal = eatenNow.carbs * 4;
    const total = proteinKcal + fatKcal + carbsKcal;

    const share = (value: number) => (total > 0 ? Math.round((value / total) * 100) : 0);

    return [
      { key: 'protein', title: 'Белки', grams: eatenNow.protein, percent: share(proteinKcal) },
      { key: 'fat', title: 'Жиры', grams: eatenNow.fat, percent: share(fatKcal) },
      { key: 'carbs', title: 'Углеводы', grams: eatenNow.carbs, percent: share(carbsKcal) },
    ];
  });

  /**
   * График веса за неделю: по каждому дню берется последний замер не позже этой
   * даты, поэтому линия непрерывная, а точки стоят только там, где был замер.
   */
  protected readonly weekChart = computed<WeekChart>(() => {
    const days: { iso: string; label: string }[] = [];
    const todayDate = new Date();

    for (let shift = 6; shift >= 0; shift -= 1) {
      const date = new Date(todayDate);
      date.setDate(todayDate.getDate() - shift);
      days.push({
        iso: toIso(date),
        label: date.toLocaleDateString('ru-RU', { weekday: 'short' }),
      });
    }

    const entries = [...this.weightList()].sort((left, right) => left.date.localeCompare(right.date));
    const values = days.map((day) => {
      const found = [...entries].reverse().find((entry) => entry.date <= day.iso);

      return found?.kg ?? null;
    });

    const measured = new Set(entries.map((entry) => entry.date));
    const known = values.filter((value): value is number => value !== null);

    if (known.length === 0) {
      return { points: [], polyline: '', area: '', ticks: [], labels: [], last: null, bubble: null };
    }

    const min = Math.min(...known) - 0.4;
    const max = Math.max(...known) + 0.4;
    const span = max - min || 1;
    const step = days.length > 1 ? (PlotRight - PlotLeft) / (days.length - 1) : 0;

    const yFor = (kg: number) => PlotBottom - ((kg - min) / span) * (PlotBottom - PlotTop);

    const points: ChartPoint[] = [];
    const labels: ChartLabel[] = [];

    days.forEach((day, index) => {
      const x = PlotLeft + index * step;

      labels.push({ x, text: day.label.charAt(0).toUpperCase() + day.label.slice(1) });

      const kg = values[index];

      if (kg !== null) {
        points.push({ id: day.iso, x, y: yFor(kg), kg, measured: measured.has(day.iso) });
      }
    });

    const polyline = points.map((point) => `${point.x.toFixed(1)},${point.y.toFixed(1)}`).join(' ');
    const first = points[0];
    const last = points[points.length - 1];
    const area = `M ${first.x.toFixed(1)},${PlotBottom} L ${points
      .map((point) => `${point.x.toFixed(1)},${point.y.toFixed(1)}`)
      .join(' L ')} L ${last.x.toFixed(1)},${PlotBottom} Z`;

    const ticks: ChartTick[] = [0, 1, 2, 3].map((index) => {
      const value = max - (span * index) / 3;

      return { y: yFor(value), label: this.formatKg(value) };
    });

    const bubble = last
      ? {
          x: Math.min(Math.max(last.x - 36, PlotLeft - 24), PlotRight - 76),
          y: Math.max(4, last.y - 42),
        }
      : null;

    return { points, polyline, area, ticks, labels, last: last ?? null, bubble };
  });

  /**
   * Изменение веса: скорость из тренда, если история набралась, иначе разница
   * первого и последнего замера в окне.
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

  protected readonly activityMinutes = computed(() =>
    (this.activity()?.items ?? []).reduce((sum, item) => sum + item.minutes, 0),
  );

  protected readonly activityItems = computed(() => this.activity()?.items ?? []);

  /** Высота столбика активности: самая длинная запись дает 100 %. */
  protected barHeight(minutes: number): number {
    const longest = Math.max(1, ...this.activityItems().map((item) => item.minutes));

    return Math.max(14, Math.round((minutes / longest) * 100));
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.error.set(null);

    const fallback = <T>(source: Observable<T>) => source.pipe(catchError(() => of(null)));

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

  protected formatSigned(value: number): string {
    const sign = value > 0 ? '+' : '';

    return `${sign}${value.toFixed(1).replace('.', ',')}`;
  }

  protected formatDelta(value: number, label: string): string {
    return `${this.formatSigned(value)} кг ${label}`;
  }

  protected formatLiters(ml: number | null | undefined): string {
    return ml === null || ml === undefined ? '—' : (ml / 1000).toFixed(1).replace('.', ',');
  }

  protected formatNumber(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : value.toLocaleString('ru-RU');
  }
}

/** Локальная дата в формате yyyy-MM-dd без сдвига часовым поясом. */
function toIso(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${date.getFullYear()}-${month}-${day}`;
}
