import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="login">
      <h1>Sign in</h1>
      <p class="hint">Demo accounts: <code>user/user</code> | <code>admin/admin</code>.</p>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field">
          <label for="username">Username</label>
          <input id="username" type="text" formControlName="username" autocomplete="username" />
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="current-password"
          />
        </div>

        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }

        <button type="submit" [disabled]="loading()">
          {{ loading() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
    </section>
  `,
  styles: `
    .login {
      max-width: 22rem;
      margin: 3rem auto;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .hint {
      color: #6b7280;
      font-size: 0.875rem;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      margin-bottom: 0.75rem;
    }
    label {
      font-weight: 500;
      color: #374151;
    }
    input {
      padding: 0.5rem 0.625rem;
      border: 1px solid #d1d5db;
      border-radius: 0.375rem;
      font-size: 1rem;
    }
    input:focus-visible {
      outline: 2px solid #2563eb;
      outline-offset: 1px;
    }
    .error {
      color: #b91c1c;
    }
    button {
      padding: 0.625rem 1.5rem;
      background-color: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      font-size: 1rem;
      font-weight: 500;
      cursor: pointer;
    }
    button:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  submit(): void {
    if (this.form.invalid) return;

    this.loading.set(true);
    this.error.set(null);
    const { username, password } = this.form.getRawValue();

    this.auth.login(username, password).subscribe({
      next: () => this.router.navigate([this.auth.isAdmin() ? '/admin' : '/query']),
      error: (err: unknown) => {
        this.error.set(this.errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  /** Only a 401 means bad credentials; everything else is a server/connectivity problem. */
  private errorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 401) return 'Invalid username or password.';
      if (err.status === 0) return 'Cannot reach the server. Is it running?';
      return `Sign-in failed — the server returned an error (${err.status}). Please try again.`;
    }
    return 'An unexpected error occurred.';
  }
}
