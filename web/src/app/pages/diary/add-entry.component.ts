import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { FoodSummary, MealKind } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { DiaryService } from './diary.service';

/**
 * Форма добавления еды в конкретный прием. Поиск показывает варианты списком:
 * пользователь сначала находит продукты, потом выбирает нужный.
 */
@Component({
  selector: 'app-add-entry',
  templateUrl: './add-entry.component.html',
  styleUrl: './add-entry.component.scss',
})
export class AddEntryComponent implements OnInit {
  private readonly diary = inject(DiaryService);
  private readonly destroyRef = inject(DestroyRef);

  /** Дата записи в формате yyyy-MM-dd. */
  readonly date = input.required<string>();

  /** Прием, в который добавляем: выбран на экране дня. */
  readonly meal = input.required<MealKind>();

  readonly added = output<void>();

  protected readonly quantity = signal(100);
  protected readonly query = signal('');
  protected readonly results = signal<FoodSummary[]>([]);
  protected readonly searched = signal(false);
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
          this.searched.set(items.length > 0 || this.query().trim().length >= 2);
        },
        error: () => {
          this.results.set([]);
          this.searched.set(false);
        },
      });
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.queries.next(value);
  }

  /** Enter в поле поиска: не ждем дебаунса, ищем сразу. */
  protected searchNow(): void {
    this.queries.next(this.query());
  }

  protected pick(food: FoodSummary): void {
    this.selected.set(food);
    this.results.set([]);
    this.searched.set(false);
    this.error.set(null);
  }

  protected clearSelection(): void {
    this.selected.set(null);
  }

  protected unitLabel(): string {
    return this.selected()?.baseUnit === 'Milliliter' ? 'мл' : 'г';
  }

  protected onQuantityInput(raw: string): void {
    const normalized = raw.replace(',', '.').trim();

    if (normalized === '') {
      this.quantity.set(0);
      return;
    }

    const value = Number(normalized);

    this.quantity.set(Number.isNaN(value) ? 0 : value);
  }

  protected submit(): void {
    const food = this.selected();

    if (food === null || this.quantity() <= 0) {
      this.error.set('Сначала найди продукт и выбери его из списка, потом укажи количество.');
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
          this.results.set([]);
          this.searched.set(false);
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
