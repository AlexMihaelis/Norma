import { Component, input } from '@angular/core';

/**
 * Фото рецепта или заглушка: цветной прямоугольник с белой тарелкой. Заглушка
 * нужна, чтобы карточки без фото не выглядели пустыми дырками.
 */
@Component({
  selector: 'app-recipe-photo',
  templateUrl: './recipe-photo.component.html',
  styleUrl: './recipe-photo.component.scss',
})
export class RecipePhotoComponent {
  readonly photo = input<string | null>(null);
  readonly name = input.required<string>();
}
