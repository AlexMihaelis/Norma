import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DiaryDay, DiaryTotals, MealKind, Plan, mealTitles } from '../../core/api.models';
import { toLocalIso } from '../../core/date';
import { describeProblem } from '../../core/problem';
import { NutritionRingComponent } from '../../ui/nutrition-ring/nutrition-ring.component';
import { AddEntryComponent } from './add-entry.component';
import { DiaryService } from './diary.service';

interface CalendarDay {
  iso: string;
  number: number;
  inMonth: boolean;
  isToday: boolean;
  isSelected: boolean;
  isFuture: boolean;
}

/**
 * Экран дня: кольцо КБЖУ, шесть приемов и стрелки по датам. День и цель
 * грузятся независимо: без профиля и веса цель не посчитать, но дневник
 * должен открываться.
 */
@Component({
  selector: 'app-diary-page',
  imports: [AddEntryComponent, RouterLink, NutritionRingComponent],
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

  /** Календарь: открыт/закрыт и какой месяц показан. */
  protected readonly calendarOpen = signal(false);
  protected readonly viewMonth = signal<Date>(this.firstOfMonth(this.startOfToday()));

  protected readonly monthLabel = computed(() => {
    const date = this.viewMonth();
    const month = date.toLocaleDateString('ru-RU', { month: 'long' });

    return `${month.charAt(0).toUpperCase()}${month.slice(1)} ${date.getFullYear()}`;
  });

  protected readonly isCurrentMonth = computed(
    () => this.viewMonth().getTime() >= this.firstOfMonth(this.startOfToday()).getTime(),
  );

  /** Сетка 6 недель с понедельника, чтобы месяцы не прыгали по высоте. */
  protected readonly calendarDays = computed<CalendarDay[]>(() => {
    const first = this.viewMonth();
    const offset = (first.getDay() + 6) % 7;
    const start = new Date(first);

    start.setDate(start.getDate() - offset);

    const todayIso = toLocalIso(this.startOfToday());
    const selectedIso = this.dateIso();
    const days: CalendarDay[] = [];

    for (let index = 0; index < 42; index += 1) {
      const date = new Date(start);
      date.setDate(start.getDate() + index);

      const iso = toLocalIso(date);

      days.push({
        iso,
        number: date.getDate(),
        inMonth: date.getMonth() === first.getMonth(),
        isToday: iso === todayIso,
        isSelected: iso === selectedIso,
        isFuture: iso > todayIso,
      });
    }

    return days;
  });

  protected readonly eaten = computed(() => this.day()?.totals.kcal ?? 0);

  /** Съеденное за день целиком: из этих граммов собирается кольцо БЖУ. */
  protected readonly totals = computed<DiaryTotals>(
    () => this.day()?.totals ?? { kcal: 0, protein: 0, fat: 0, carbs: 0 },
  );

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
  protected toggleCalendar(event: MouseEvent): void {
    event.stopPropagation();
    this.viewMonth.set(this.firstOfMonth(this.selected()));
    this.calendarOpen.update((open) => !open);
  }

  @HostListener('document:click')
  protected closeCalendar(): void {
    this.calendarOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  protected closeCalendarOnEscape(): void {
    this.calendarOpen.set(false);
  }

  protected previousMonth(): void {
    const date = new Date(this.viewMonth());
    date.setMonth(date.getMonth() - 1);
    this.viewMonth.set(date);
  }

  protected nextMonth(): void {
    if (this.isCurrentMonth()) {
      return;
    }

    const date = new Date(this.viewMonth());
    date.setMonth(date.getMonth() + 1);
    this.viewMonth.set(date);
  }

  protected pickDay(day: CalendarDay, event: MouseEvent): void {
    event.stopPropagation();

    if (day.isFuture) {
      return;
    }

    const [year, month, date] = day.iso.split('-').map(Number);

    this.selected.set(new Date(year, month - 1, date));
    this.calendarOpen.set(false);
    this.load();
  }

  protected pickToday(event: MouseEvent): void {
    event.stopPropagation();
    this.selected.set(this.startOfToday());
    this.calendarOpen.set(false);
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

  private firstOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), 1);
  }
}
