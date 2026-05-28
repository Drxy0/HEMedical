import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, forkJoin } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import {
  ClinicalMeasurementType,
  MEASUREMENT_LABELS,
  MEASUREMENT_UNITS,
} from '../../shared/models/clinical-measurement.model';
import { VerificationService } from '../../shared/services/verification.service';
import { StatisticsService } from '../../shared/services/statistics.service';

type QueryType = 'date' | 'age';

function rangeRequired(group: AbstractControl): ValidationErrors | null {
  const queryType = group.get('queryType')?.value;
  if (queryType === 'date') {
    const start = group.get('startDate')?.value as string;
    const end = group.get('endDate')?.value as string;
    if (!start || !end) return { datesRequired: true };
  }
  if (queryType === 'age') {
    const start = group.get('startAge')?.value;
    const end = group.get('endAge')?.value;
    if (start == null || end == null) return { agesRequired: true };
  }
  return null;
}

interface DateResult {
  readonly type: 'date';
  readonly verification: number;
  readonly statistics: number;
}

interface AgeResult {
  readonly type: 'age';
  readonly statistics: number;
}

type QueryResult = DateResult | AgeResult;

@Component({
  selector: 'app-clinical-query',
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './clinical-query.component.html',
  styleUrl: './clinical-query.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClinicalQueryComponent {
  private readonly fb = inject(FormBuilder);
  private readonly verificationService = inject(VerificationService);
  private readonly statisticsService = inject(StatisticsService);

  readonly form = this.fb.group(
    {
      measurementType: [ClinicalMeasurementType.BloodPressure as ClinicalMeasurementType, Validators.required],
      queryType: ['date' as QueryType],
      startDate: [''],
      endDate: [''],
      startAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      endAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
    },
    { validators: rangeRequired },
  );

  readonly queryType = toSignal(this.form.controls.queryType.valueChanges, {
    initialValue: 'date' as QueryType,
  });

  private readonly measurementTypeValue = toSignal(
    this.form.controls.measurementType.valueChanges,
    { initialValue: ClinicalMeasurementType.BloodPressure },
  );

  readonly isLoading = signal(false);
  readonly submitted = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<QueryResult | null>(null);

  readonly measurementTypeOptions = [
    { value: ClinicalMeasurementType.BloodPressure, label: MEASUREMENT_LABELS[ClinicalMeasurementType.BloodPressure] },
    { value: ClinicalMeasurementType.HbA1c, label: MEASUREMENT_LABELS[ClinicalMeasurementType.HbA1c] },
  ];

  readonly unit = computed(() => MEASUREMENT_UNITS[this.measurementTypeValue()!] ?? '');

  onSubmit(): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const { measurementType, queryType, startDate, endDate, startAge, endAge } =
      this.form.getRawValue();

    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    if (queryType === 'date') {
      forkJoin({
        verification: this.verificationService.getAverageByDateRange(
          measurementType!,
          startDate || undefined,
          endDate || undefined,
        ),
        statistics: this.statisticsService.getAverageByDateRange(
          measurementType!,
          startDate || undefined,
          endDate || undefined,
        ),
      })
        .pipe(
          catchError((err: unknown) => {
            this.error.set(this.extractErrorMessage(err));
            return EMPTY;
          }),
          finalize(() => this.isLoading.set(false)),
        )
        .subscribe(({ verification, statistics }) => {
          this.result.set({ type: 'date', verification, statistics });
        });
    } else {
      this.statisticsService
        .getAverageByAgeRange(measurementType!, startAge ?? undefined, endAge ?? undefined)
        .pipe(
          catchError((err: unknown) => {
            this.error.set(this.extractErrorMessage(err));
            return EMPTY;
          }),
          finalize(() => this.isLoading.set(false)),
        )
        .subscribe((statistics) => {
          this.result.set({ type: 'age', statistics });
        });
    }
  }

  private extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const problem = err.error as { detail?: string; title?: string } | null;
      return problem?.detail ?? problem?.title ?? `Error ${err.status}: ${err.statusText}`;
    }
    return 'An unexpected error occurred';
  }
}
