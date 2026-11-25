import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DiaryDay, MealKind, Plan, mealTitles } from '../../core/api.models';
import { toLocalIso } from '../../core/date';
import { describeProblem } from '../../core/problem';
import { AddEntryComponent } from './add-entry.component';
import { DiaryService } from './diary.service';

interface RingSegment {
  color: string;
  dash: string;
  offset: number;
}

/**
 * Экран дня: кольцо КБЖУ, шесть приемов и стрелки по датам. День и цель
 * грузятся независимо: без профиля и веса цель не посчитать, но дневник
 * должен открываться.
 */
@Component({
  selector: 'app-diary-page',
  imports: [AddEntryComponent, RouterLink],
  templateUrl: './diary.page.html',
  styleUrl: './diary.page.scss',
})
export class DiaryPage implements OnInit {
  private readonly diary = inject(DiaryService);

  protected readonly day = signal<DiaryDay | null>(null);
  protected readonly plan = signal<Plan | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly planError = signal<string | null>(null);
  protected readonly needsSetup = signal(false);
  protected readonly openMeal = signal<MealKind | null>(null);
  protected readonly titles = mealTitles;

  /** Выбранная дата: по умолчанию сегодня, стрелки ходят в прошлое. */
  protected readonly selected = signal<Date>(this.startOfToday());
  protected readonly dateIso = computed(() => toLocalIso(this.selected()));
  protected readonly dateLabel = computed(() => this.formatDateLabel(this.selected()));
  protected readonly isToday = computed(() => this.dateIso() === toLocalIso(this.startOfToday()));

  /** Верхняя граница календаря: будущие дни выбирать незачем. */
  protected readonly maxDateIso = toLocalIso(this.startOfToday());

  private readonly ringRadius = 46;
  private readonly ringLength = 2 * Math.PI * 46;

  protected readonly eaten = computed(() => this.day()?.totals.kcal ?? 0);

  protected readonly remainingKcal = computed(() => {
    const target = this.plan()?.targetKcal;

    return target === null || target === undefined ? null : Math.round(target - this.eaten());
  });

  protected readonly remainingMacros = computed(() => {
    const macros = this.plan()?.macros;
    const totals = this.day()?.totals;

    if (!macros || !totals) {
      return null;
    }

    return {
      protein: Math.max(0, Math.round(macros.proteinG - totals.protein)),
      fat: Math.max(0, Math.round(macros.fatG - totals.fat)),
      carbs: Math.max(0, Math.round(macros.carbsG - totals.carbs)),
    };
  });

  protected readonly ringSegments = computed<RingSegment[]>(() => {
    const macros = this.plan()?.macros;
    const totals = this.day()?.totals;

    if (!macros || !totals) {
      return [];
    }

    const shares = [
      { color: 'var(--color-protein)', value: this.share(totals.protein, macros.proteinG) },
      { color: 'var(--color-fat-pastel)', value: this.share(totals.fat, macros.fatG) },
      { color: 'var(--color-carbs-pastel)', value: this.share(totals.carbs, macros.carbsG) },
    ];

    let sent = 0;

    return shares.map((share) => {
      const length = share.value * this.ringLength;
      const segment = {
        color: share.color,
        dash: `${length.toFixed(2)} ${(this.ringLength - length).toFixed(2)}`,
        offset: -sent,
      };

      sent += length;

      return segment;
    });
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.error.set(null);

    this.diary.day(this.dateIso()).subscribe({
      next: (day) => this.day.set(day),
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });

    this.diary.plan().subscribe({
      next: (plan) => {
        this.plan.set(plan);
        this.planError.set(null);
        this.needsSetup.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.plan.set(null);

        // 400 значит «профиль не заполнен»: сервер перечисляет поля, но
        // пользователю полезнее ссылки на экраны, где их заполнить.
        if (error.status === 400) {
          this.needsSetup.set(true);
          this.planError.set('Цель не рассчитана: заполни профиль и запиши вес.');
        } else {
          this.needsSetup.set(false);
          this.planError.set(describeProblem(error));
        }
      },
    });
  }

  protected previousDay(): void {
    const date = new Date(this.selected());
    date.setDate(date.getDate() - 1);
    this.selected.set(date);
    this.load();
  }

  /** Клик по дате открывает календарь: за неделю-две так быстрее, чем стрелками. */
  protected openDatePicker(input: HTMLInputElement): void {
    const picker = input as HTMLInputElement & { showPicker?: () => void };

    if (typeof picker.showPicker === 'function') {
      picker.showPicker();
    } else {
      input.focus();
      input.click();
    }
  }

  protected onDatePicked(value: string): void {
    if (value === '' || value > this.maxDateIso) {
      return;
    }

    const [year, month, day] = value.split('-').map(Number);

    this.selected.set(new Date(year, month - 1, day));
    this.load();
  }

  protected nextDay(): void {
    if (this.isToday()) {
      return;
    }

    const date = new Date(this.selected());
    date.setDate(date.getDate() + 1);
    this.selected.set(date);
    this.load();
  }

  protected today(): void {
    this.selected.set(this.startOfToday());
    this.load();
  }

  protected toggle(kind: MealKind): void {
    this.openMeal.set(this.openMeal() === kind ? null : kind);
  }

  protected remove(entryId: string): void {
    this.diary.remove(entryId).subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected unitLabel(unit: 'Gram' | 'Milliliter'): string {
    return unit === 'Milliliter' ? 'мл' : 'г';
  }

  protected formatNumber(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : value.toLocaleString('ru-RU');
  }

  /** «Среда, 25 ноября»: день недели в макете с большой буквы. */
  private formatDateLabel(date: Date): string {
    const weekday = date.toLocaleDateString('ru-RU', { weekday: 'long' });
    const rest = date.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });

    return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${rest}`;
  }

  private startOfToday(): Date {
    const now = new Date();

    return new Date(now.getFullYear(), now.getMonth(), now.getDate());
  }

  private share(value: number, target: number): number {
    if (target <= 0) {
      return 0;
    }

    return Math.min(1, value / target);
  }
}
