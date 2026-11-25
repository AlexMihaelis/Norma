import { Component, effect, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccountService } from './core/account.service';
import { AuthService } from './core/auth.service';

/** Оболочка приложения: шапка с навигацией, аккаунтом и место для экрана. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly account = inject(AccountService);
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

  protected signOut(): void {
    this.auth.logout();
    this.account.reset();
    void this.router.navigateByUrl('/login');
  }
}