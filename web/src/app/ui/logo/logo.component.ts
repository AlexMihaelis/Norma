/**
 * Логотип Norma: знак-лист с лентой и сердцем плюс подпись. Компактный вариант
 * показывает только знак - для сайдбара и узких экранов.
 *
 * Файл лежит в `public/logo.png`: исходник 1278x1231, для веба уменьшен до
 * 320 px по высоте и обрезан по прозрачным полям.
 */
import { Component, input } from '@angular/core';

@Component({
  selector: 'app-logo',
  templateUrl: './logo.component.html',
  styleUrl: './logo.component.scss',
})
export class LogoComponent {
  /** Только знак, без подписи. */
  readonly compact = input(false);

  /** Размер: `md` - шапка и крупные места, `sm` - сайдбар. */
  readonly size = input<'md' | 'sm'>('md');
}
