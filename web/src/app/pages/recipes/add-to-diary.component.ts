import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MealKind, Recipe, mealTitles } from '../../core/api.models';
import { toLocalIso } from '../../core/date';
import { describeProblem } from '../../core/problem';
import { RecipesService } from './recipes.service';

/** Модалка «записать в дневник»: граммовка и прием пищи. */
@Component({
  selector: 'app-add-to-diary',
  imports: [DecimalPipe],
  templateUrl: './add-to-diary.component.html',
  styleUrl: './add-to-diary.component.scss',
})
export class AddToDiaryComponent {
  private readonly recipes = inject(RecipesService);

  readonly recipe = input.required<Recipe>();

  readonly added = output<void>();
  readonly closed = output<void>();

  protected readonly meals = mealTitles;
  protected readonly mealList = Object.keys(mealTitles) as MealKind[];

  protected readonly grams = signal(150);
  protected readonly meal = signal<MealKind>('Lunch');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly kcal = computed(() =>
    Math.round((this.recipe().kcalPer100g * this.grams()) / 100),
  );

  protected onGrams(raw: string): void {
    const parsed = Number(raw.replace(',', '.').trim());

    this.grams.set(Number.isFinite(parsed) ? Math.max(0, parsed) : 0);
  }

  protected onMeal(raw: string): void {
    this.meal.set(raw as MealKind);
  }

  protected save(): void {
    if (this.grams() < 1) {
      this.error.set('Граммовка должна быть не меньше грамма.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.recipes
      .addToDiary(this.recipe().id, this.grams(), this.meal(), toLocalIso(new Date()))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.added.emit();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(describeProblem(error));
        },
      });
  }

  protected close(): void {
    this.closed.emit();
  }
}