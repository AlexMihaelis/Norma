import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
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

interface ChartDay {
  iso: string;
  label: string;
  weekday: string;
  isMonthStart: boolean;
  kcal: number | null;
  targetKcal: number | null;
  diffKcal: number | null;
}

/** Экран статистики: средние, столбцы по дням и таблица дней за период. */
@Component({
  selector: 'app-stats-page',
  templateUrl: './stats.page.html',
  styleUrl: './stats.page.scss',
})
export class StatsPage implements OnInit {
  private readonly stats = inject(StatsService);

  /** Лента дней: прокручиваем к сегодняшнему дню, как только она отрисовалась. */
  @ViewChild('stripRef')
  private set stripRef(ref: ElementRef<HTMLDivElement> | undefined) {
    if (!ref) {
      return;
    }

    requestAnimationFrame(() => {
      ref.nativeElement.scrollLeft = 0;
    });
  }

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

  /**
   * Лента всех дней периода, а не только дней с записями: подписи должны быть
   * всегда, а пустой день просто без столбца.
   */
  protected readonly chartDays = computed<ChartDay[]>(() => {
    const value = this.data();

    if (!value) {
      return [];
    }

    const byDate = new Map(value.days.map((day) => [day.date, day]));
    const result: ChartDay[] = [];
    const cursor = new Date(`${value.from}T00:00:00`);
    const end = new Date(`${value.to}T00:00:00`);

    while (cursor <= end) {
      const iso = toLocalIso(cursor);
      const day = byDate.get(iso);
      const isMonthStart = cursor.getDate() === 1 || result.length === 0;
      const monthShort = cursor.toLocaleDateString('ru-RU', { month: 'short' });

      result.push({
        iso,
        label:
          this.period() === 7
            ? cursor.toLocaleDateString('ru-RU', { weekday: 'short' })
            : isMonthStart
              ? `${cursor.getDate()} ${monthShort}`
              : String(cursor.getDate()),
        weekday: cursor.toLocaleDateString('ru-RU', { weekday: 'short' }),
        isMonthStart,
        kcal: day?.kcal ?? null,
        targetKcal: day?.targetKcal ?? null,
        diffKcal: day?.diffKcal ?? null,
      });

      cursor.setDate(cursor.getDate() + 1);
    }

    // Длинные периоды листаются с сегодняшнего дня: он первый слева, прошлое - справа.
    return this.period() === 7 ? result : result.reverse();
  });

  protected readonly target = computed(() => this.data()?.averages.targetKcal ?? null);

  /** Высота пунктирной линии цели в процентах от шкалы. */
  protected readonly targetPercent = computed(() => {
    const target = this.target();

    return target === null ? null : Math.min(100, Math.round((target / this.scale()) * 100));
  });

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

  protected isOver(day: ChartDay): boolean {
    return day.diffKcal !== null && day.diffKcal > 0;
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
