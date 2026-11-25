import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { FoodSummary, MealKind, mealTitles } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { DiaryService } from './diary.service';

/**
 * Форма «добавить еду»: поиск продукта с задержкой, количество и прием.
 * После успешной записи сообщает родителю, чтобы тот перечитал день.
 */
@Component({
  selector: 'app-add-entry',
  templateUrl: './add-entry.component.html',
  styleUrl: './add-entry.component.scss',
})
export class AddEntryComponent implements OnInit {
  private readonly diary = inject(DiaryService);
  private readonly destroyRef = inject(DestroyRef);

  /** Дата, к которой относится запись, в формате yyyy-MM-dd. */
  readonly date = input.required<string>();

  /** Событие «позиция добавлена»: родитель перечитывает день. */
  readonly added = output<void>();

  protected readonly titles = mealTitles;
  protected readonly mealKinds: MealKind[] = [
    'Breakfast',
    'MorningSnack',
    'Lunch',
    'AfternoonSnack',
    'Dinner',
    'EveningSnack',
  ];

  protected readonly meal = signal<MealKind>('Breakfast');
  protected readonly quantity = signal(100);
  protected readonly query = signal('');
  protected readonly results = signal<FoodSummary[]>([]);
  protected readonly noResults = signal(false);
  protected readonly selected = signal<FoodSummary | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly queries = new Subject<string>();

  ngOnInit(): void {
    this.queries
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((query) => {
          const trimmed = query.trim();

          return trimmed.length < 2
            ? of<FoodSummary[]>([])
            : this.diary.search(trimmed).pipe(map((response) => response.items));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (items) => {
          this.results.set(items);
          this.noResults.set(
            items.length === 0 && this.query().trim().length >= 2 && this.selected() === null,
          );
        },
        error: () => {
          this.results.set([]);
          this.noResults.set(false);
        },
      });
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.noResults.set(false);
    this.queries.next(value);
  }

  protected pick(food: FoodSummary): void {
    this.selected.set(food);
    this.query.set(food.name);
    this.results.set([]);
    this.noResults.set(false);
  }

  protected unitLabel(): string {
    return this.selected()?.baseUnit === 'Milliliter' ? 'мл' : 'г';
  }

  protected submit(): void {
    const food = this.selected();

    if (food === null || this.quantity() <= 0) {
      this.error.set('Выбери продукт из подсказок (нажми на строку в списке) и укажи количество больше нуля.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.diary
      .add({
        meal: this.meal(),
        foodId: food.id,
        quantity: this.quantity(),
        date: this.date(),
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.selected.set(null);
          this.query.set('');
          this.quantity.set(100);
          this.added.emit();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(describeProblem(error));
        },
      });
  }
}
