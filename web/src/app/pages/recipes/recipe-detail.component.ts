import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Recipe } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { AddToDiaryComponent } from './add-to-diary.component';
import { RecipePhotoComponent } from './recipe-photo.component';
import { RecipesService } from './recipes.service';

/**
 * Карточка рецепта: фото, состав, инструкция и кнопки. Открывается поверх
 * списка, поэтому не теряет место прокрутки под ним.
 */
@Component({
  selector: 'app-recipe-detail',
  imports: [DecimalPipe, RecipePhotoComponent, AddToDiaryComponent, RouterLink],
  templateUrl: './recipe-detail.component.html',
  styleUrl: './recipe-detail.component.scss',
})
export class RecipeDetailComponent {
  private readonly recipes = inject(RecipesService);

  readonly recipe = input.required<Recipe>();
  /** Подписи меток: код с сервера на русский переводит только справочник. */
  readonly titles = input.required<Record<string, string>>();

  /** Список изменился: избранное, правка или удаление. */
  readonly changed = output<void>();
  readonly edited = output<Recipe>();
  readonly closed = output<void>();
  /** Блюдо записано в дневник. */
  readonly added = output<void>();

  protected readonly current = signal<Recipe | null>(null);
  protected readonly addOpen = signal(false);
  protected readonly addedNotice = signal(false);
  protected readonly confirmDelete = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly tagList = computed(() => {
    const recipe = this.current();

    if (!recipe) {
      return [];
    }

    const titles = this.titles();

    return [...recipe.autoTags, ...recipe.tags]
      .map((code) => titles[code])
      .filter((title): title is string => Boolean(title));
  });

  constructor() {
    // Внешний рецепт приходит из списка; после правки родитель присылает новый.
    effect(() => this.current.set(this.recipe()));
  }

  protected toggleFavorite(): void {
    const recipe = this.current();

    if (!recipe) {
      return;
    }

    this.busy.set(true);

    this.recipes.setFavorite(recipe.id, !recipe.isFavorite).subscribe({
      next: () => {
        this.current.set({ ...recipe, isFavorite: !recipe.isFavorite });
        this.busy.set(false);
        this.changed.emit();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected edit(): void {
    const recipe = this.current();

    if (recipe) {
      this.edited.emit(recipe);
    }
  }

  protected remove(): void {
    const recipe = this.current();

    if (!recipe) {
      return;
    }

    this.busy.set(true);

    this.recipes.remove(recipe.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.changed.emit();
        this.closed.emit();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.confirmDelete.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected openAdd(): void {
    this.addOpen.set(true);
  }

  protected onAdded(): void {
    this.addOpen.set(false);
    this.addedNotice.set(true);
    this.added.emit();
  }

  protected close(): void {
    this.closed.emit();
  }
}