import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccountService } from './core/account.service';
import { AuthService } from './core/auth.service';
import { LogoComponent } from './ui/logo/logo.component';

interface NavItem {
  path: string;
  title: string;
  icon: string;
  exact?: boolean;
}

/** Оболочка приложения: сайдбар с разделами, шапка с логотипом и место для экрана. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LogoComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly account = inject(AccountService);
  protected readonly menuOpen = signal(false);
  private readonly router = inject(Router);

  /** Разделы сайдбара. Иконки - пути SVG в сетке 24x24, рисуются штрихом. */
  protected readonly nav: NavItem[] = [
    {
      path: '/',
      title: 'Главная',
      exact: true,
      icon: 'M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z',
    },
    {
      path: '/diary',
      title: 'Питание',
      icon: 'M7 3v6a2 2 0 0 0 4 0V3M9 9v12M17 3c-2 2.2-2.5 4.2-2.5 6 0 1.6.8 2.5 2.5 2.5s2.5-.9 2.5-2.5c0-1.8-.5-3.8-2.5-6zM17 11.5V21',
    },
    {
      path: '/recipes',
      title: 'Рецепты',
      icon: 'M5 4.5A1.5 1.5 0 0 1 6.5 3H19v18H6.5A1.5 1.5 0 0 1 5 19.5zM9 7h7M9 11h7M9 15h4',
    },
    {
      path: '/stats',
      title: 'Статистика',
      icon: 'M5 20V10M12 20V4M19 20v-7',
    },
    {
      path: '/weight',
      title: 'Мой прогресс',
      icon: 'M4 16.5 9 11l4 3 7-8M4 20h16',
    },
    {
      path: '/profile',
      title: 'Профиль',
      icon: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4.5 20.5c.8-3.8 3.9-5.5 7.5-5.5s6.7 1.7 7.5 5.5',
    },
  ];

  /** Настройки пока без экрана: пункт виден, но неактивен. */
  protected readonly settingsIcon =
    'M12 15.2a3.2 3.2 0 1 0 0-6.4 3.2 3.2 0 0 0 0 6.4zM19.4 13a7.7 7.7 0 0 0 0-2l2-1.5-2-3.4-2.3 1a7.6 7.6 0 0 0-1.7-1L15 3.5h-4l-.4 2.6c-.6.3-1.2.6-1.7 1l-2.3-1-2 3.4L6.6 11a7.7 7.7 0 0 0 0 2l-2 1.5 2 3.4 2.3-1c.5.4 1.1.7 1.7 1l.4 2.6h4l.4-2.6c.6-.3 1.2-.6 1.7-1l2.3 1 2-3.4z';

  protected readonly moonIcon = 'M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z';

  protected readonly sunIcon =
    'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM12 2v2M12 20v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M2 12h2M20 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4';

  protected readonly logoutIcon = 'M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 8l-4 4 4 4M6 12h9';

  protected readonly calendarIcon =
    'M4 7a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zM8 3v4M16 3v4M4 11h16';

  protected readonly bellIcon =
    'M12 4a5 5 0 0 0-5 5v3l-1.5 3h13L17 12V9a5 5 0 0 0-5-5zM10 18a2 2 0 0 0 4 0';

  /** Дата в шапке: «Суббота, 27 ноября 2026». */
  protected readonly todayLabel = this.formatToday();

  private formatToday(): string {
    const date = new Date();
    const weekday = date.toLocaleDateString('ru-RU', { weekday: 'long' });
    const rest = date
      .toLocaleDateString('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' })
      .replace(' г.', '');

    return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${rest}`;
  }

  constructor() {
    // Вошли или вышли - подтягиваем (или чистим) ник и аватар для шапки.
    effect(() => {
      if (this.auth.signedIn()) {
        this.account.load().subscribe();
      } else {
        this.account.reset();
      }
    });
  }

  /** Меню аккаунта: клик по нику или аватару. Клик по ссылке меню его закрывает. */
  protected toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen.update((open) => !open);
  }

  @HostListener('document:click')
  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  protected closeMenuOnEscape(): void {
    this.menuOpen.set(false);
  }

  protected signOut(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    this.account.reset();
    void this.router.navigateByUrl('/login');
  }
}
