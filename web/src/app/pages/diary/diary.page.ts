import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DiaryDay, Plan, mealTitles } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { AddEntryComponent } from './add-entry.component';
import { DiaryService } from './diary.service';

/**
 * Экран дня: цель на сегодня, записи по приемам и добавление еды.
 *
 * День и цель грузятся независимо: без профиля и веса цель не посчитать, но
 * дневник должен открываться - иначе экран выглядит сломанным.
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
    this.error.set(null);

    this.diary.day(this.dateIso).subscribe({
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
