import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { DiaryDay, Plan, mealTitles } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { DiaryService } from './diary.service';

/** Экран дня: цель на сегодня и записи по приемам, пока только чтение. */
@Component({
  selector: 'app-diary-page',
  templateUrl: './diary.page.html',
  styleUrl: './diary.page.scss',
})
export class DiaryPage implements OnInit {
  private readonly diary = inject(DiaryService);

  protected readonly day = signal<DiaryDay | null>(null);
  protected readonly plan = signal<Plan | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly titles = mealTitles;

  protected readonly today = new Date();
  protected readonly dateLabel = this.today.toLocaleDateString('ru-RU', {
    day: 'numeric',
    month: 'long',
    weekday: 'long',
  });

  ngOnInit(): void {
    const date = this.today.toISOString().slice(0, 10);

    forkJoin({
      day: this.diary.day(date),
      plan: this.diary.plan(),
    }).subscribe({
      next: ({ day, plan }) => {
        this.day.set(day);
        this.plan.set(plan);
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected unitLabel(unit: 'Gram' | 'Milliliter'): string {
    return unit === 'Milliliter' ? 'мл' : 'г';
  }

  protected progressPercent(): number {
    const target = this.plan()?.targetKcal;
    const eaten = this.day()?.totals.kcal ?? 0;

    if (!target || target <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((eaten / target) * 100));
  }
}
