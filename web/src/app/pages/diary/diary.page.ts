import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { DiaryDay, Plan, mealTitles } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { AddEntryComponent } from './add-entry.component';
import { DiaryService } from './diary.service';

/** Экран дня: цель на сегодня, записи по приемам и добавление еды. */
@Component({
  selector: 'app-diary-page',
  imports: [AddEntryComponent],
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
  protected readonly dateIso = this.today.toISOString().slice(0, 10);
  protected readonly dateLabel = this.today.toLocaleDateString('ru-RU', {
    day: 'numeric',
    month: 'long',
    weekday: 'long',
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    forkJoin({
      day: this.diary.day(this.dateIso),
      plan: this.diary.plan(),
    }).subscribe({
      next: ({ day, plan }) => {
        this.day.set(day);
        this.plan.set(plan);
        this.error.set(null);
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
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

  protected progressPercent(): number {
    const target = this.plan()?.targetKcal;
    const eaten = this.day()?.totals.kcal ?? 0;

    if (!target || target <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((eaten / target) * 100));
  }
}
