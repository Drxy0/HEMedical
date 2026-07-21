import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { LoincCredentialsService } from '../../shared/services/loinc-credentials.service';

/**
 * Modal shown when a query fails because the Client has no LOINC credentials.
 * Collects a loinc.org username/password, sends them to be validated + stored, and
 * emits {@link saved} on success (the caller then retries the original query).
 */
@Component({
  selector: 'app-loinc-credentials-dialog',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'cancel()' },
  template: `
    <div class="overlay" (click)="onOverlayClick($event)">
      <div
        class="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="loinc-dialog-title"
        aria-describedby="loinc-dialog-desc"
      >
        <h2 id="loinc-dialog-title">LOINC credentials needed</h2>
        <p id="loinc-dialog-desc" class="desc">
          Measurement codes are validated against the LOINC terminology server
          (<code>fhir.loinc.org</code>), which requires a free
          <a href="https://loinc.org" target="_blank" rel="noopener">loinc.org</a> account.
          Enter yours to run queries.
        </p>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="loinc-username">LOINC username</label>
            <input
              id="loinc-username"
              #firstField
              type="text"
              formControlName="username"
              autocomplete="username"
            />
          </div>
          <div class="field">
            <label for="loinc-password">LOINC password</label>
            <input
              id="loinc-password"
              type="password"
              formControlName="password"
              autocomplete="current-password"
            />
          </div>

          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }

          <div class="actions">
            <button type="button" class="secondary" (click)="cancel()" [disabled]="loading()">
              Cancel
            </button>
            <button type="submit" [disabled]="loading()">
              {{ loading() ? 'Verifying…' : 'Save & retry' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: `
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(17, 24, 39, 0.55);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      z-index: 50;
    }
    .dialog {
      background: #fff;
      border-radius: 0.5rem;
      padding: 1.5rem;
      width: 100%;
      max-width: 26rem;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.25);
    }
    h2 {
      margin: 0 0 0.5rem;
      font-size: 1.15rem;
    }
    .desc {
      color: #4b5563;
      font-size: 0.875rem;
      margin: 0 0 1rem;
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
      font-size: 0.875rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      margin-top: 1rem;
    }
    button {
      padding: 0.5rem 1rem;
      border-radius: 0.375rem;
      border: none;
      font-size: 0.95rem;
      font-weight: 500;
      cursor: pointer;
      background: #2563eb;
      color: #fff;
    }
    button.secondary {
      background: #e5e7eb;
      color: #374151;
    }
    button:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }
  `,
})
export class LoincCredentialsDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly loinc = inject(LoincCredentialsService);

  /** Emitted once credentials are accepted and stored. */
  readonly saved = output<void>();
  /** Emitted when the user dismisses the dialog without saving. */
  readonly cancelled = output<void>();

  private readonly firstField = viewChild<ElementRef<HTMLInputElement>>('firstField');

  readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  constructor() {
    afterNextRender(() => this.firstField()?.nativeElement.focus());
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { username, password } = this.form.getRawValue();

    this.loinc.save(username, password).subscribe({
      next: () => {
        this.loading.set(false);
        this.saved.emit();
      },
      error: (err: unknown) => {
        this.error.set(this.message(err));
        this.loading.set(false);
      },
    });
  }

  cancel(): void {
    if (this.loading()) return;
    this.cancelled.emit();
  }

  onOverlayClick(event: MouseEvent): void {
    // Only a click on the backdrop itself (not the dialog) dismisses.
    if (event.target === event.currentTarget) this.cancel();
  }

  private message(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const problem = err.error as { detail?: string } | null;
      if (err.status === 424 || err.status === 400)
        return problem?.detail ?? 'The LOINC server rejected these credentials.';
      if (err.status === 401) return 'You must be signed in to set LOINC credentials.';
      if (err.status === 0) return 'Cannot reach the server. Is it running?';
      return problem?.detail ?? `Failed to save credentials (error ${err.status}).`;
    }
    return 'An unexpected error occurred.';
  }
}
