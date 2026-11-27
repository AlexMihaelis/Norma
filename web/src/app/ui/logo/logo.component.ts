import { Component, input } from '@angular/core';

/**
 * Логотип Norma: три листа, знак N с градиентом и подпись. Компактный вариант
 * показывает только знак - для сайдбара и узких экранов.
 */
@Component({
  selector: 'app-logo',
  templateUrl: './logo.component.html',
  styleUrl: './logo.component.scss',
})
export class LogoComponent {
  /** Только знак, без подписи. */
  readonly compact = input(false);
}
