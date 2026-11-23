import { Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { describeProblem } from '../../core/problem';

/** Вход и регистрация на одном экране: переключатель режима. */
@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly mode = signal<'login' | 'register'>('login');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] }),
  });

  protected toggleMode(): void {
    this.mode.set(this.mode() === 'login' ? 'register' : 'login');
    this.error.set(null);
  }

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    const request = this.mode() === 'login'
      ? this.auth.login(email, password)
      : this.auth.register(email, password);

    request.subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: (error: HttpErrorResponse) => {
        // Сервер намеренно отвечает одинаковым 401 на «нет такого email» и
        // «неверный пароль», чтобы перебором нельзя было узнать, кто зарегистрирован.
        // Поэтому подсказываем оба варианта, а не гадаем.
        this.error.set(
          error.status === 401
            ? 'Неверный email или пароль. Если аккаунта ещё нет - нажми «Зарегистрироваться».'
            : describeProblem(error),
        );
        this.busy.set(false);
      },
    });
  }
}
