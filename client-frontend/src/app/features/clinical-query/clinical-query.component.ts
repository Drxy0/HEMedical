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
import { StatisticsChartComponent } from './statistics-chart.component';

type QueryType = 'date' | 'age';

export const CUSTOM_LOINC = 'CustomLoinc' as const;
type MeasurementSelection = ClinicalMeasurementType | typeof CUSTOM_LOINC;

/** LOINC codes are digits, a hyphen, and a single check digit (e.g. 4548-4). */
const LOINC_CODE_PATTERN = /^\d+-\d$/;

function rangeRequired(group: AbstractControl): ValidationErrors | null {
  const errors: ValidationErrors = {};
  const queryType = group.get('queryType')?.value;
  if (queryType === 'date') {
    const start = group.get('startDate')?.value as string;
    const end = group.get('endDate')?.value as string;
    if (!start || !end) errors['datesRequired'] = true;
    else if (start > end) errors['dateRangeInvalid'] = true;
  }
  if (queryType === 'age') {
    const start = group.get('startAge')?.value;
    const end = group.get('endAge')?.value;
    if (start == null || end == null) errors['agesRequired'] = true;
  }
  if (group.get('measurementType')?.value === CUSTOM_LOINC) {
    const code = ((group.get('loincCode')?.value as string | null) ?? '').trim();
    if (!code) errors['loincRequired'] = true;
    else if (!LOINC_CODE_PATTERN.test(code)) errors['loincInvalid'] = true;
  }
  return Object.keys(errors).length ? errors : null;
}

@Component({
  selector: 'app-clinical-query',
  imports: [ReactiveFormsModule, DecimalPipe, StatisticsChartComponent],
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
        ClinicalMeasurementType.BloodPressure as MeasurementSelection,
        Validators.required,
      ],
      loincCode: [null as string | null],
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
    { initialValue: ClinicalMeasurementType.BloodPressure as MeasurementSelection },
  );

  readonly isCustomLoinc = computed(() => this.measurementTypeValue() === CUSTOM_LOINC);

  readonly isLoadingHE = signal(false);
  readonly isLoadingPlaintext = signal(false);
  readonly submitted = signal(false);
  readonly heError = signal<string | null>(null);
  readonly plaintextError = signal<string | null>(null);
  readonly heResult = signal<QueryResult[] | null>(null);
  readonly plaintextResult = signal<QueryResult[] | null>(null);

  /** True while the results panel has nothing to show (only rendered on wide screens). */
  readonly showResultsPlaceholder = computed(
    () =>
      !this.isLoadingHE() &&
      !this.isLoadingPlaintext() &&
      this.heResult() === null &&
      this.plaintextResult() === null &&
      !this.heError() &&
      !this.plaintextError(),
  );

  readonly measurementTypeOptions: { value: MeasurementSelection; label: string }[] = [
    { value: ClinicalMeasurementType.BloodPressure, label: 'Blood Pressure' },
    { value: ClinicalMeasurementType.HbA1c, label: 'HbA1c' },
    { value: CUSTOM_LOINC, label: 'Custom LOINC code…' },
  ];

  readonly sexOptions = [
    { value: PatientSex.Male, label: SEX_LABELS[PatientSex.Male] },
    { value: PatientSex.Female, label: SEX_LABELS[PatientSex.Female] },
    { value: PatientSex.Other, label: SEX_LABELS[PatientSex.Other] },
  ];


  onQueryHE(): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const {
      measurementType,
      loincCode,
      queryType,
      startDate,
      endDate,
      startAge,
      endAge,
      patientSex,
    } = this.form.getRawValue();

    const sex = patientSex ?? undefined;

    this.isLoadingHE.set(true);
    this.heError.set(null);
    this.heResult.set(null);

    let request$;
    if (measurementType === CUSTOM_LOINC) {
      const code = loincCode!.trim();
      request$ =
        queryType === 'date'
          ? this.queryHEService.getAverageByLoincDateRange(
              code,
              startDate ?? undefined,
              endDate ?? undefined,
              sex,
            )
          : this.queryHEService.getAverageByLoincAgeRange(code, startAge!, endAge!, sex);
    } else {
      request$ =
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
    }

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
    // Plaintext verification queries only support the predefined measurement types.
    if (this.form.invalid || this.isCustomLoinc()) return;

    const { measurementType, queryType, startDate, endDate, startAge, endAge, patientSex } =
      this.form.getRawValue();
    const sex = patientSex ?? undefined;

    this.isLoadingPlaintext.set(true);
    this.plaintextError.set(null);
    this.plaintextResult.set(null);

    const type = measurementType as ClinicalMeasurementType;
    const request$ =
      queryType === 'date'
        ? this.queryPlaintextService.getAverageByDateRange(
            type,
            startDate ?? undefined,
            endDate ?? undefined,
            sex,
          )
        : this.queryPlaintextService.getAverageByAgeRange(
            type,
            startAge ?? undefined,
            endAge ?? undefined,
            sex,
          );

    request$.pipe(
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
