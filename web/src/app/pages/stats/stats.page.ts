import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { catchError, forkJoin, of } from 'rxjs';
import { Plan, Stats, StatsDay } from '../../core/api.models';
import { toLocalIso } from '../../core/date';
import { describeProblem } from '../../core/problem';
import { StatsService } from './stats.service';

interface PeriodOption {
  days: number;
  title: string;
}

interface SummaryItem {
  label: string;
  value: number | null;
  target: number | null;
  unit: string;
}

/** Экран статистики: средние, столбцы по дням и таблица дней за период. */
@Component({
  selector: 'app-stats-page',
  templateUrl: './stats.page.html',
  styleUrl: './stats.page.scss',
})
export class StatsPage implements OnInit {
  private readonly stats = inject(StatsService);

  protected readonly periods: PeriodOption[] = [
    { days: 7, title: '7 дней' },
    { days: 30, title: '30 дней' },
    { days: 365, title: 'Год' },
  ];

  protected readonly period = signal(30);
  protected readonly data = signal<Stats | null>(null);
  protected readonly plan = signal<Plan | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly days = computed(() => this.data()?.days ?? []);

  /** Для столбцов берем не больше 14 последних дней, иначе они становятся нечитаемыми. */
  protected readonly barDays = computed(() => this.days().slice(-14));

  protected readonly scale = computed(() => {
    const values = this.days().map((day) => day.kcal);
    const target = this.data()?.averages.targetKcal ?? 0;

    return Math.max(1, target, ...values);
  });

  protected readonly summary = computed<SummaryItem[]>(() => {
    const averages = this.data()?.averages;
    const macros = this.plan()?.macros;

    if (!averages) {
      return [];
    }

    return [
      { label: 'Средние калории', value: averages.kcal, target: averages.targetKcal, unit: 'ккал' },
      { label: 'Средние белки', value: averages.protein, target: macros?.proteinG ?? null, unit: 'г' },
      { label: 'Средние жиры', value: averages.fat, target: macros?.fatG ?? null, unit: 'г' },
      { label: 'Средние углеводы', value: averages.carbs, target: macros?.carbsG ?? null, unit: 'г' },
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  protected setPeriod(days: number): void {
    this.period.set(days);
    this.load();
  }

  protected load(): void {
    const to = new Date();
    const from = new Date();
    from.setDate(to.getDate() - (this.period() - 1));

    this.error.set(null);

    forkJoin({
      stats: this.stats.get(toLocalIso(from), toLocalIso(to)),
      plan: this.stats.plan().pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ stats, plan }) => {
        this.data.set(stats);
        this.plan.set(plan);
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected barHeight(kcal: number): number {
    return Math.max(2, Math.round((kcal / this.scale()) * 100));
  }

  protected isOverTarget(day: StatsDay): boolean {
    return day.diffKcal !== null && day.diffKcal > 0;
  }

  protected weekday(iso: string): string {
    return new Date(iso).toLocaleDateString('ru-RU', { weekday: 'short' });
  }

  protected formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });
  }

  protected format(value: number | null | undefined, digits = 0): string {
    if (value === null || value === undefined) {
      return '—';
    }

    return value.toFixed(digits).replace('.', ',');
  }

  protected formatNumber(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : Math.round(value).toLocaleString('ru-RU');
  }

  protected bju(day: StatsDay): string {
    return `${Math.round(day.protein)} / ${Math.round(day.fat)} / ${Math.round(day.carbs)}`;
  }
}
