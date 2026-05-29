import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { EMPTY } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import {
  ClinicalMeasurementType,
  PatientSex,
  QueryResult,
  SEX_LABELS,
} from '../../shared/models/clinical-measurement.model';
import { QueryHEService } from '../../shared/services/query-he.service';
import { QueryPlaintextService } from '../../shared/services/query-plaintext.service';

type QueryType = 'date' | 'age';

function rangeRequired(group: AbstractControl): ValidationErrors | null {
  const queryType = group.get('queryType')?.value;
  if (queryType === 'date') {
    const start = group.get('startDate')?.value as string;
    const end = group.get('endDate')?.value as string;
    if (!start || !end) return { datesRequired: true };
    if (start > end) return { dateRangeInvalid: true };
  }
  if (queryType === 'age') {
    const start = group.get('startAge')?.value;
    const end = group.get('endAge')?.value;
    if (start == null || end == null) return { agesRequired: true };
  }
  return null;
}

@Component({
  selector: 'app-clinical-query',
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './clinical-query.component.html',
  styleUrl: './clinical-query.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClinicalQueryComponent {
  private readonly fb = inject(FormBuilder);
  private readonly queryHEService = inject(QueryHEService);
  private readonly queryPlaintextService = inject(QueryPlaintextService);

  readonly form = this.fb.group(
    {
      measurementType: [
        ClinicalMeasurementType.BloodPressure as ClinicalMeasurementType,
        Validators.required,
      ],
      queryType: ['date' as QueryType],
      startDate: [null as string | null],
      endDate: [null as string | null],
      startAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      endAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      patientSex: [null as PatientSex | null],
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

  readonly isLoadingHE = signal(false);
  readonly isLoadingPlaintext = signal(false);
  readonly submitted = signal(false);
  readonly heError = signal<string | null>(null);
  readonly plaintextError = signal<string | null>(null);
  readonly heResult = signal<QueryResult[] | null>(null);
  readonly plaintextResult = signal<QueryResult[] | null>(null);

  readonly measurementTypeOptions = [
    { value: ClinicalMeasurementType.BloodPressure, label: 'Blood Pressure' },
    { value: ClinicalMeasurementType.HbA1c, label: 'HbA1c' },
  ];

  readonly sexOptions = [
    { value: PatientSex.Male, label: SEX_LABELS[PatientSex.Male] },
    { value: PatientSex.Female, label: SEX_LABELS[PatientSex.Female] },
    { value: PatientSex.Other, label: SEX_LABELS[PatientSex.Other] },
  ];


  onQueryHE(): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const { measurementType, queryType, startDate, endDate, startAge, endAge, patientSex } =
      this.form.getRawValue();

    const sex = patientSex ?? undefined;

    this.isLoadingHE.set(true);
    this.heError.set(null);
    this.heResult.set(null);

    const request$ =
      queryType === 'date'
        ? this.queryHEService.getAverageByDateRange(
            measurementType!,
            startDate ?? undefined,
            endDate ?? undefined,
            sex,
          )
        : this.queryHEService.getAverageByAgeRange(
            measurementType!,
            startAge ?? undefined,
            endAge ?? undefined,
            sex,
          );

    request$
      .pipe(
        catchError((err: unknown) => {
          this.heError.set(this.extractErrorMessage(err));
          return EMPTY;
        }),
        finalize(() => this.isLoadingHE.set(false)),
      )
      .subscribe((result) => this.heResult.set(result));
  }

  onQueryPlaintext(): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const { measurementType, startDate, endDate, patientSex } = this.form.getRawValue();
    const sex = patientSex ?? undefined;

    this.isLoadingPlaintext.set(true);
    this.plaintextError.set(null);
    this.plaintextResult.set(null);

    this.queryPlaintextService
      .getAverageByDateRange(measurementType!, startDate ?? undefined, endDate ?? undefined, sex)
      .pipe(
        catchError((err: unknown) => {
          this.plaintextError.set(this.extractErrorMessage(err));
          return EMPTY;
        }),
        finalize(() => this.isLoadingPlaintext.set(false)),
      )
      .subscribe((result) => this.plaintextResult.set(result));
  }

  private extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const problem = err.error as { detail?: string; title?: string } | null;
      return problem?.detail ?? problem?.title ?? `Error ${err.status}: ${err.statusText}`;
    }
    return 'An unexpected error occurred';
  }
}
