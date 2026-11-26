import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { FoodSummary, Recipe, RecipeRequest, RecipeTag } from '../../core/api.models';
import { preparePhoto } from '../../core/image-file';
import { describeProblem } from '../../core/problem';
import { RecipePhotoComponent } from './recipe-photo.component';
import { RecipesService } from './recipes.service';

interface IngredientRow {
  food: FoodSummary;
  grams: number;
}

/**
 * Форма рецепта: название, фото, состав с граммовками, инструкция и метки.
 * Используется и для создания, и для правки - поля те же, отличается только
 * вызов API.
 */
@Component({
  selector: 'app-recipe-form',
  imports: [DecimalPipe, RecipePhotoComponent],
  templateUrl: './recipe-form.component.html',
  styleUrl: './recipe-form.component.scss',
})
export class RecipeFormComponent implements OnInit {
  private readonly recipes = inject(RecipesService);
  private readonly destroyRef = inject(DestroyRef);

  /** Рецепт для правки; <c>null</c> - создаем новый. */
  readonly recipe = input<Recipe | null>(null);
  readonly catalog = input.required<RecipeTag[]>();

  readonly saved = output<Recipe>();
  readonly closed = output<void>();

  protected readonly name = signal('');
  protected readonly instructions = signal('');
  protected readonly photo = signal<string | null>(null);
  protected readonly tags = signal<string[]>([]);
  protected readonly rows = signal<IngredientRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly photoBusy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly query = signal('');
  protected readonly results = signal<FoodSummary[]>([]);

  private readonly queries = new Subject<string>();

  /** Ручные метки по группам: приемы пищи, тип блюда, способ приготовления. */
  protected readonly groups = computed(() => {
    const groups = new Map<string, RecipeTag[]>();

    for (const tag of this.catalog()) {
      if (tag.auto) {
        continue;
      }

      const list = groups.get(tag.group) ?? [];
      list.push(tag);
      groups.set(tag.group, list);
    }

    return [...groups.entries()].map(([title, items]) => ({ title, items }));
  });

  protected readonly totals = computed(() => {
    let grams = 0;
    let kcal = 0;
    let protein = 0;
    let fat = 0;
    let carbs = 0;

    for (const row of this.rows()) {
      const factor = row.grams / 100;

      grams += row.grams;
      kcal += row.food.kcalPer100g * factor;
      protein += row.food.proteinPer100g * factor;
      fat += row.food.fatPer100g * factor;
      carbs += row.food.carbsPer100g * factor;
    }

    return {
      grams,
      kcal,
      protein,
      fat,
      carbs,
      kcalPer100: grams > 0 ? (kcal / grams) * 100 : 0,
      proteinPer100: grams > 0 ? (protein / grams) * 100 : 0,
      fatPer100: grams > 0 ? (fat / grams) * 100 : 0,
      carbsPer100: grams > 0 ? (carbs / grams) * 100 : 0,
    };
  });

  ngOnInit(): void {
    const current = this.recipe();

    if (current) {
      this.name.set(current.name);
      this.instructions.set(current.instructions ?? '');
      this.photo.set(current.photoDataUrl);
      this.tags.set([...current.tags]);
      this.rows.set(
        current.ingredients.map((ingredient) => ({
          grams: ingredient.grams,
          food: {
            id: ingredient.foodId,
            name: ingredient.name,
            state: 'Unknown',
            baseUnit: 'Gram',
            kcalPer100g: ingredient.kcalPer100g,
            proteinPer100g: ingredient.proteinPer100g,
            fatPer100g: ingredient.fatPer100g,
            carbsPer100g: ingredient.carbsPer100g,
          },
        })),
      );
    }

    this.queries
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((query) => {
          const trimmed = query.trim();

          return trimmed.length < 2
            ? of<FoodSummary[]>([])
            : this.recipes.searchFoods(trimmed).pipe(map((response) => response.items));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (items) => this.results.set(items),
        error: () => this.results.set([]),
      });
  }

  protected onName(value: string): void {
    this.name.set(value);
  }

  protected onInstructions(value: string): void {
    this.instructions.set(value);
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.queries.next(value);
  }

  protected addFood(food: FoodSummary): void {
    const exists = this.rows().some((row) => row.food.id === food.id);

    if (exists) {
      this.error.set('Этот продукт уже есть в составе.');
      return;
    }

    this.rows.update((rows) => [...rows, { food, grams: 100 }]);
    this.query.set('');
    this.results.set([]);
    this.error.set(null);
  }

  protected onGrams(index: number, raw: string): void {
    const parsed = Number(raw.replace(',', '.').trim());
    const grams = Number.isFinite(parsed) ? Math.max(0, parsed) : 0;

    this.rows.update((rows) =>
      rows.map((row, position) => (position === index ? { ...row, grams } : row)),
    );
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, position) => position !== index));
  }

  protected toggleTag(code: string): void {
    this.tags.update((list) =>
      list.includes(code) ? list.filter((item) => item !== code) : [...list, code],
    );
  }

  protected onPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    input.value = '';

    if (!file) {
      return;
    }

    this.photoBusy.set(true);
    this.error.set(null);

    preparePhoto(file)
      .then((dataUrl) => this.photo.set(dataUrl))
      .catch((reason: Error) => this.error.set(reason.message))
      .finally(() => this.photoBusy.set(false));
  }

  protected clearPhoto(): void {
    this.photo.set(null);
  }

  protected save(): void {
    const request: RecipeRequest = {
      name: this.name().trim(),
      instructions: this.instructions().trim() || null,
      tags: this.tags(),
      photoDataUrl: this.photo(),
      ingredients: this.rows().map((row) => ({ foodId: row.food.id, grams: row.grams })),
    };

    if (request.name.length < 2) {
      this.error.set('Название должно быть хотя бы из двух символов.');
      return;
    }

    if (request.ingredients.length === 0) {
      this.error.set('Добавь хотя бы один продукт в состав.');
      return;
    }

    if (request.ingredients.some((ingredient) => ingredient.grams < 1)) {
      this.error.set('Вес каждого продукта должен быть не меньше грамма.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const current = this.recipe();
    const call = current
      ? this.recipes.update(current.id, request)
      : this.recipes.create(request);

    call.subscribe({
      next: (recipe) => {
        this.busy.set(false);
        this.saved.emit(recipe);
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
