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
  BreakdownResult,
  HistogramResult,
  MEASUREMENT_PRESETS,
  MeasurementQuery,
  PatientSex,
  QueryResult,
  SEX_LABELS,
} from '../../shared/models/clinical-measurement.model';
import { QueryHEService } from '../../shared/services/query-he.service';
import { QueryPlaintextService } from '../../shared/services/query-plaintext.service';
import { StatisticsChartComponent } from './statistics-chart.component';
import { BreakdownChartComponent } from './breakdown-chart.component';
import { HistogramChartComponent } from './histogram-chart.component';

type QueryType = 'date' | 'age';
type ResultType = 'summary' | 'breakdown' | 'histogram';

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
  if (group.get('resultType')?.value === 'histogram') {
    const binStart = group.get('binStart')?.value;
    const binWidth = group.get('binWidth')?.value;
    const binCount = group.get('binCount')?.value;
    if (binStart == null || binWidth == null || binCount == null) errors['binsRequired'] = true;
    else if (binWidth <= 0 || binCount < 1 || binCount > 512) errors['binsInvalid'] = true;
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
  imports: [
    ReactiveFormsModule,
    DecimalPipe,
    StatisticsChartComponent,
    BreakdownChartComponent,
    HistogramChartComponent,
  ],
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
      resultType: ['summary' as ResultType],
      queryType: ['date' as QueryType],
      startDate: [null as string | null],
      endDate: [null as string | null],
      startAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      endAge: [null as number | null, [Validators.min(0), Validators.max(150)]],
      ageBucketSize: [10 as number, [Validators.min(1), Validators.max(150)]],
      dateBucketMonths: [12 as number],
      threshold: [null as number | null],
      binStart: [null as number | null],
      binWidth: [null as number | null],
      binCount: [10 as number | null, [Validators.min(1), Validators.max(512)]],
      patientSex: [null as PatientSex | null],
    },
    { validators: validateQueryForm },
  );

  readonly queryType = toSignal(this.form.controls.queryType.valueChanges, {
    initialValue: 'date' as QueryType,
  });

  readonly resultType = toSignal(this.form.controls.resultType.valueChanges, {
    initialValue: 'summary' as ResultType,
  });

  readonly isBreakdown = computed(() => this.resultType() === 'breakdown');
  readonly isHistogram = computed(() => this.resultType() === 'histogram');
  readonly isSummary = computed(() => this.resultType() === 'summary');

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
  readonly heBreakdown = signal<BreakdownResult | null>(null);
  readonly plaintextBreakdown = signal<BreakdownResult | null>(null);
  readonly heHistogram = signal<HistogramResult | null>(null);
  readonly plaintextHistogram = signal<HistogramResult | null>(null);

  /** True while the results panel has nothing to show (only rendered on wide screens). */
  readonly showResultsPlaceholder = computed(
    () =>
      !this.isLoadingHE() &&
      !this.isLoadingPlaintext() &&
      this.heResult() === null &&
      this.plaintextResult() === null &&
      this.heBreakdown() === null &&
      this.plaintextBreakdown() === null &&
      this.heHistogram() === null &&
      this.plaintextHistogram() === null &&
      !this.heError() &&
      !this.plaintextError(),
  );

  readonly groupByOptions = [
    { value: 12, label: 'Year' },
    { value: 3, label: 'Quarter' },
    { value: 1, label: 'Month' },
  ];

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
    this.clearHe();
    if (this.isBreakdown())
      this.runBreakdown(this.queryHEService, this.isLoadingHE, this.heError, this.heBreakdown);
    else if (this.isHistogram())
      this.runHistogram(this.queryHEService, this.isLoadingHE, this.heError, this.heHistogram);
    else this.runQueries(this.queryHEService, this.isLoadingHE, this.heError, this.heResult);
  }

  onQueryPlaintext(): void {
    this.clearPlaintext();
    if (this.isBreakdown())
      this.runBreakdown(
        this.queryPlaintextService,
        this.isLoadingPlaintext,
        this.plaintextError,
        this.plaintextBreakdown,
      );
    else if (this.isHistogram())
      this.runHistogram(
        this.queryPlaintextService,
        this.isLoadingPlaintext,
        this.plaintextError,
        this.plaintextHistogram,
      );
    else
      this.runQueries(
        this.queryPlaintextService,
        this.isLoadingPlaintext,
        this.plaintextError,
        this.plaintextResult,
      );
  }

  private clearHe(): void {
    this.heResult.set(null);
    this.heBreakdown.set(null);
    this.heHistogram.set(null);
  }

  private clearPlaintext(): void {
    this.plaintextResult.set(null);
    this.plaintextBreakdown.set(null);
    this.plaintextHistogram.set(null);
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
    const { queryType, startDate, endDate, startAge, endAge, patientSex, threshold } =
      this.form.getRawValue();
    const sex = patientSex ?? undefined;
    const thr = threshold ?? undefined;
    return queryType === 'date'
      ? service.getStatisticsByDateRange(
          query.loincCode,
          query.componentLoincCode,
          startDate ?? undefined,
          endDate ?? undefined,
          sex,
          thr,
        )
      : service.getStatisticsByAgeRange(
          query.loincCode,
          query.componentLoincCode,
          startAge!,
          endAge!,
          sex,
          thr,
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

  /**
   * Breakdown mode: fan out the ordinary average query over buckets. Uses the first
   * measurement of the selection (e.g. systolic for the blood-pressure preset), since a
   * breakdown plots one measurement across many buckets.
   */
  private runBreakdown(
    service: QueryHEService | QueryPlaintextService,
    loading: WritableSignal<boolean>,
    error: WritableSignal<string | null>,
    result: WritableSignal<BreakdownResult | null>,
  ): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const query = this.buildQueries()[0];
    if (!query) return;

    const { queryType, startDate, endDate, startAge, endAge, ageBucketSize, dateBucketMonths, patientSex } =
      this.form.getRawValue();
    const sex = patientSex ?? undefined;

    loading.set(true);
    error.set(null);
    result.set(null);

    const request$ =
      queryType === 'date'
        ? service.getBreakdownByDate(
            query.loincCode,
            query.componentLoincCode,
            startDate!,
            endDate!,
            dateBucketMonths!,
            sex,
          )
        : service.getBreakdownByAge(
            query.loincCode,
            query.componentLoincCode,
            startAge!,
            endAge!,
            ageBucketSize!,
            sex,
          );

    request$
      .pipe(
        map((r) => (query.label ? { ...r, measurementName: query.label } : r)),
        catchError((err: unknown) => {
          error.set(this.extractErrorMessage(err));
          return EMPTY;
        }),
        finalize(() => loading.set(false)),
      )
      .subscribe((r) => result.set(r));
  }

  /**
   * Frequency histogram mode: one round trip carrying the whole histogram (unlike the
   * breakdown, which is one query per bucket). Uses the first measurement of the selection,
   * since a histogram plots the value distribution of one measurement.
   */
  private runHistogram(
    service: QueryHEService | QueryPlaintextService,
    loading: WritableSignal<boolean>,
    error: WritableSignal<string | null>,
    result: WritableSignal<HistogramResult | null>,
  ): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const query = this.buildQueries()[0];
    if (!query) return;

    const { queryType, startDate, endDate, startAge, endAge, binStart, binWidth, binCount, patientSex } =
      this.form.getRawValue();
    const sex = patientSex ?? undefined;

    loading.set(true);
    error.set(null);
    result.set(null);

    const request$ =
      queryType === 'date'
        ? service.getHistogramByDateRange(
            query.loincCode,
            query.componentLoincCode,
            startDate ?? undefined,
            endDate ?? undefined,
            sex,
            binStart!,
            binWidth!,
            binCount!,
          )
        : service.getHistogramByAgeRange(
            query.loincCode,
            query.componentLoincCode,
            startAge!,
            endAge!,
            sex,
            binStart!,
            binWidth!,
            binCount!,
          );

    request$
      .pipe(
        map((r) => (query.label ? { ...r, measurementName: query.label } : r)),
        catchError((err: unknown) => {
          error.set(this.extractErrorMessage(err));
          return EMPTY;
        }),
        finalize(() => loading.set(false)),
      )
      .subscribe((r) => result.set(r));
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
