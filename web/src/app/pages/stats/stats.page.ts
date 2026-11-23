import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Stats, StatsDay } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { StatsService } from './stats.service';

interface PeriodOption {
  days: number;
  title: string;
}

/** Экран статистики: средние по дням с записями и отклонение от цели. */
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
    { days: 90, title: '90 дней' },
  ];

  protected readonly period = signal(7);
  protected readonly data = signal<Stats | null>(null);
  protected readonly error = signal<string | null>(null);

  /** Масштаб столбцов: самый калорийный день или цель, что больше. */
  protected readonly scale = computed(() => {
    const value = this.data();
    const days = value?.days.map((day) => day.kcal) ?? [];
    const target = value?.averages.targetKcal ?? 0;

    return Math.max(1, target, ...days);
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

    this.stats.get(toIso(from), toIso(to)).subscribe({
      next: (value) => this.data.set(value),
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected barHeight(kcal: number): number {
    return Math.max(2, Math.round((kcal / this.scale()) * 100));
  }

  protected isOverTarget(day: StatsDay): boolean {
    return day.diffKcal !== null && day.diffKcal > 0;
  }

  protected format(value: number | null | undefined, digits = 0): string {
    if (value === null || value === undefined) {
      return '—';
    }

    return value.toFixed(digits).replace('.', ',');
  }

  protected formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'short',
    });
  }

  protected formatDateLong(iso: string): string {
    return new Date(iso).toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }

  protected deviationText(deviation: { diffKcal: number; diffPercent: number }): string {
    const direction = deviation.diffKcal >= 0 ? 'выше цели' : 'ниже цели';

    return `${direction} на ${Math.abs(deviation.diffKcal)} ккал (${this.format(
      Math.abs(deviation.diffPercent),
      1,
    )}%)`;
  }
}

/** Дата в формате yyyy-MM-dd без сдвига часового пояса. */
function toIso(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);

  return local.toISOString().slice(0, 10);
}
