import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { describeProblem } from '../../core/problem';

/**
 * Вход и регистрация на одном экране. После первой регистрации ведем в профиль:
 * без него не посчитать цель, а дневник без цели выглядит сломанным.
 */
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
  protected readonly showPassword = signal(false);

  /** Четность переключателя: меняет класс, чтобы анимация запускалась заново. */
  protected readonly swap = signal(0);
  protected readonly isRegister = () => this.mode() === 'register';

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] }),
  });

  protected toggleMode(): void {
    this.mode.set(this.mode() === 'login' ? 'register' : 'login');
    this.swap.update((value) => value + 1);
    this.error.set(null);
    this.showPassword.set(false);

    // Пароль между режимами не переносим: на экране регистрации он должен быть пустым.
    this.form.controls.password.setValue('');
  }

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    const isRegister = this.isRegister();
    const request = isRegister ? this.auth.register(email, password) : this.auth.login(email, password);

    request.subscribe({
      next: () => this.router.navigateByUrl(isRegister ? '/profile' : '/'),
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
