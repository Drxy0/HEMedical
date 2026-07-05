import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  WritableSignal,
} from '@angular/core';
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
import { EMPTY, forkJoin, Observable } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import {
  MEASUREMENT_PRESETS,
  MeasurementQuery,
  PatientSex,
  QueryResult,
  SEX_LABELS,
} from '../../shared/models/clinical-measurement.model';
import { QueryHEService } from '../../shared/services/query-he.service';
import { QueryPlaintextService } from '../../shared/services/query-plaintext.service';
import { StatisticsChartComponent } from './statistics-chart.component';

type QueryType = 'date' | 'age';

const CUSTOM_LOINC = 'custom' as const;

/** LOINC codes are digits, a hyphen, and a single check digit (e.g. 4548-4). */
const LOINC_CODE_PATTERN = /^\d+-\d$/;

/** Cross-field validation: the required range for the chosen query type, plus LOINC code formats in custom mode. */
function validateQueryForm(group: AbstractControl): ValidationErrors | null {
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
  if (group.get('measurementSelection')?.value === CUSTOM_LOINC) {
    const code = ((group.get('loincCode')?.value as string | null) ?? '').trim();
    if (!code) errors['loincRequired'] = true;
    else if (!LOINC_CODE_PATTERN.test(code)) errors['loincInvalid'] = true;

    const componentCode = ((group.get('componentLoincCode')?.value as string | null) ?? '').trim();
    if (componentCode && !LOINC_CODE_PATTERN.test(componentCode))
      errors['componentLoincInvalid'] = true;
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
      measurementSelection: [MEASUREMENT_PRESETS[0].id, Validators.required],
      loincCode: [null as string | null],
      componentLoincCode: [null as string | null],
      queryType: ['date' as QueryType],
      startDate: [null as string | null],
      endDate: [null as string | null],
      startAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      endAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      patientSex: [null as PatientSex | null],
    },
    { validators: validateQueryForm },
  );

  readonly queryType = toSignal(this.form.controls.queryType.valueChanges, {
    initialValue: 'date' as QueryType,
  });

  private readonly measurementSelection = toSignal(
    this.form.controls.measurementSelection.valueChanges,
    { initialValue: MEASUREMENT_PRESETS[0].id },
  );

  readonly isCustomLoinc = computed(() => this.measurementSelection() === CUSTOM_LOINC);

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

  readonly measurementOptions = [
    ...MEASUREMENT_PRESETS.map((p) => ({ value: p.id, label: p.label })),
    { value: CUSTOM_LOINC, label: 'Custom LOINC code…' },
  ];

  readonly sexOptions = [
    { value: PatientSex.Male, label: SEX_LABELS[PatientSex.Male] },
    { value: PatientSex.Female, label: SEX_LABELS[PatientSex.Female] },
    { value: PatientSex.Other, label: SEX_LABELS[PatientSex.Other] },
  ];

  onQueryHE(): void {
    this.runQueries(this.queryHEService, this.isLoadingHE, this.heError, this.heResult);
  }

  onQueryPlaintext(): void {
    this.runQueries(
      this.queryPlaintextService,
      this.isLoadingPlaintext,
      this.plaintextError,
      this.plaintextResult,
    );
  }

  /** Resolves the selected preset (or the custom inputs) into the backend queries to run. */
  private buildQueries(): MeasurementQuery[] {
    const { measurementSelection, loincCode, componentLoincCode } = this.form.getRawValue();

    if (measurementSelection === CUSTOM_LOINC) {
      const componentCode = componentLoincCode?.trim();
      return [
        {
          loincCode: loincCode!.trim(),
          componentLoincCode: componentCode ? componentCode : undefined,
        },
      ];
    }

    return MEASUREMENT_PRESETS.find((p) => p.id === measurementSelection)?.queries ?? [];
  }

  private buildRequest(
    service: QueryHEService | QueryPlaintextService,
    query: MeasurementQuery,
  ): Observable<QueryResult> {
    const { queryType, startDate, endDate, startAge, endAge, patientSex } =
      this.form.getRawValue();
    const sex = patientSex ?? undefined;
    return queryType === 'date'
      ? service.getStatisticsByDateRange(
          query.loincCode,
          query.componentLoincCode,
          startDate ?? undefined,
          endDate ?? undefined,
          sex,
        )
      : service.getStatisticsByAgeRange(
          query.loincCode,
          query.componentLoincCode,
          startAge!,
          endAge!,
          sex,
        );
  }

  private runQueries(
    service: QueryHEService | QueryPlaintextService,
    loading: WritableSignal<boolean>,
    error: WritableSignal<string | null>,
    result: WritableSignal<QueryResult[] | null>,
  ): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const queries = this.buildQueries();
    if (!queries.length) return;

    loading.set(true);
    error.set(null);
    result.set(null);

    forkJoin(
      queries.map((q) =>
        this.buildRequest(service, q).pipe(map((r) => this.applyDisplayOverrides(q, r))),
      ),
    )
      .pipe(
        catchError((err: unknown) => {
          error.set(this.extractErrorMessage(err));
          return EMPTY;
        }),
        finalize(() => loading.set(false)),
      )
      .subscribe((results) => result.set(results));
  }

  /** Preset labels/units win over the backend-provided ones; custom queries keep the backend's. */
  private applyDisplayOverrides(query: MeasurementQuery, result: QueryResult): QueryResult {
    return {
      ...result,
      measurementName: query.label ?? result.measurementName,
      unitOfMeasurement: query.unit ?? result.unitOfMeasurement,
    };
  }

  private extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const problem = err.error as { detail?: string; title?: string } | null;
      return problem?.detail ?? problem?.title ?? `Error ${err.status}: ${err.statusText}`;
    }
    return 'An unexpected error occurred';
  }
}
