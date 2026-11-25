import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccountService } from './core/account.service';
import { AuthService } from './core/auth.service';

/** Оболочка приложения: шапка с навигацией, меню аккаунта и место для экрана. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly account = inject(AccountService);
  protected readonly menuOpen = signal(false);
  private readonly router = inject(Router);

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
