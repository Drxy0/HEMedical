import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
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
import { HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, Observable } from 'rxjs';
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
import { AuthService } from '../../shared/services/auth.service';
import { QueryResultsComponent } from './query-results.component';
import { LoincCredentialsDialogComponent } from './loinc-credentials-dialog.component';

/** HTTP 424 (Failed Dependency): the Client needs LOINC credentials before it can verify codes. */
const LOINC_CREDENTIALS_REQUIRED_STATUS = 424;

type QueryType = 'date' | 'age';
type ResultType = 'summary' | 'breakdown' | 'histogram';

/** LOINC codes are digits, a hyphen, and a single check digit (e.g. 4548-4). */
const LOINC_CODE_PATTERN = /^\d+-\d$/;

/** The "nice" step sizes (1-2-5 sequence across magnitudes) offered for histogram ranges. */
const NICE_STEP_MULTIPLIERS = [1, 2, 2.5, 5];

/**
 * Candidate step sizes for splitting [start, end] into ranges, picked from the standard
 * 1-2-5 sequence and filtered to ones giving a sensible number of ranges (2 to 200) —
 * the same approach charting libraries use to pick readable axis ticks.
 */
function niceStepOptions(span: number): number[] {
  if (!span || span <= 0) return [];
  const options: number[] = [];
  for (let exponent = -3; exponent <= 4; exponent++) {
    const magnitude = 10 ** exponent;
    for (const multiplier of NICE_STEP_MULTIPLIERS) {
      const step = multiplier * magnitude;
      const rangeCount = span / step;
      if (rangeCount >= 2 && rangeCount <= 200) options.push(step);
    }
  }
  return [...new Set(options)].sort((a, b) => a - b);
}

/** The number of histogram ranges [start, end] splits into at the given step, clamped to what the API allows. */
function computeBinCount(
  start: number | null,
  end: number | null,
  width: number | null,
): number | null {
  if (start == null || end == null || !width || end <= start) return null;
  return Math.max(1, Math.min(512, Math.round((end - start) / width)));
}

function roundForDisplay(value: number): number {
  return Math.round(value * 1e6) / 1e6;
}

/** Cross-field validation: the LOINC codes plus the required range for the chosen query type. */
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
    const binEnd = group.get('binEnd')?.value;
    const binWidth = group.get('binWidth')?.value;
    if (binStart == null || binEnd == null || binWidth == null) errors['binsRequired'] = true;
    else if (binEnd <= binStart || computeBinCount(binStart, binEnd, binWidth) == null)
      errors['binsInvalid'] = true;
  }

  const code = ((group.get('loincCode')?.value as string | null) ?? '').trim();
  if (!code) errors['loincRequired'] = true;
  else if (!LOINC_CODE_PATTERN.test(code)) errors['loincInvalid'] = true;

  const componentCode = ((group.get('componentLoincCode')?.value as string | null) ?? '').trim();
  if (componentCode && !LOINC_CODE_PATTERN.test(componentCode))
    errors['componentLoincInvalid'] = true;

  return Object.keys(errors).length ? errors : null;
}

@Component({
  selector: 'app-clinical-query',
  imports: [ReactiveFormsModule, QueryResultsComponent, LoincCredentialsDialogComponent],
  templateUrl: './query.component.html',
  styleUrl: './query.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClinicalQueryComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly queryHEService = inject(QueryHEService);
  private readonly queryPlaintextService = inject(QueryPlaintextService);

  readonly form = this.fb.group(
    {
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
      includeStandardDeviation: [false],
      binStart: [null as number | null],
      binEnd: [null as number | null],
      binWidth: [null as number | null],
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
  readonly canQueryPlaintext = computed(() => this.auth.isAdmin());

  private readonly binStartValue = toSignal(this.form.controls.binStart.valueChanges, {
    initialValue: null as number | null,
  });
  private readonly binEndValue = toSignal(this.form.controls.binEnd.valueChanges, {
    initialValue: null as number | null,
  });
  private readonly binWidthValue = toSignal(this.form.controls.binWidth.valueChanges, {
    initialValue: null as number | null,
  });

  /** Step choices offered for the current start/end span (empty until both are filled in). */
  readonly binWidthOptions = computed(() => {
    const start = this.binStartValue();
    const end = this.binEndValue();
    if (start == null || end == null || end <= start) return [];
    return niceStepOptions(end - start);
  });

  /** Number of ranges [start, end] splits into at the chosen step — never entered directly by the user. */
  private readonly derivedBinCount = computed(() =>
    computeBinCount(this.binStartValue(), this.binEndValue(), this.binWidthValue()),
  );

  readonly binRangesPreview = computed(() => {
    const start = this.binStartValue();
    const width = this.binWidthValue();
    const count = this.derivedBinCount();
    if (start == null || !width || !count) return null;

    const shown = Math.min(count, 3);
    const ranges = Array.from(
      { length: shown },
      (_, i) => `${roundForDisplay(start + i * width)}–${roundForDisplay(start + (i + 1) * width)}`,
    );
    const suffix = count > shown ? ' …' : '';
    return `Patients will be counted per range: ${ranges.join(', ')}${suffix} (${count} range${count === 1 ? '' : 's'} total)`;
  });

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

  /** Shown when a query fails with 424 because the Client has no LOINC credentials. */
  readonly showLoincDialog = signal(false);
  /** The query to re-run, and the error signal to report to, once credentials are entered. */
  private pendingRetry: (() => void) | null = null;
  private pendingErrorSignal: WritableSignal<string | null> | null = null;

  readonly groupByOptions = [
    { value: 12, label: 'Year' },
    { value: 3, label: 'Quarter' },
    { value: 1, label: 'Month' },
  ];

  /**
   * Example measurements offered by the "fill from example" dropdown. The presets are
   * flattened to individual codes, since the form now describes one measurement at a time.
   */
  readonly presetFills: MeasurementQuery[] = MEASUREMENT_PRESETS.flatMap((p) => p.queries);

  readonly sexOptions = [
    { value: PatientSex.Male, label: SEX_LABELS[PatientSex.Male] },
    { value: PatientSex.Female, label: SEX_LABELS[PatientSex.Female] },
    { value: PatientSex.Other, label: SEX_LABELS[PatientSex.Other] },
  ];

  constructor() {
    // A step chosen for a previous start/end span may no longer be offered once that
    // span changes; clear it rather than silently keeping a now-unlisted selection.
    effect(() => {
      const options = this.binWidthOptions();
      const current = this.form.controls.binWidth.value;
      if (current != null && !options.includes(current)) {
        this.form.controls.binWidth.setValue(null);
      }
    });
  }

  /** Fills the LOINC code fields from the chosen example measurement (a convenience only). */
  applyPreset(index: string): void {
    if (index === '') return;
    const query = this.presetFills[Number(index)];
    if (!query) return;
    this.form.patchValue({
      loincCode: query.loincCode,
      componentLoincCode: query.componentLoincCode ?? null,
    });
  }

  onQueryHE(): void {
    this.pendingRetry = () => this.onQueryHE();
    this.clearHeForQuery();
    if (this.isBreakdown())
      this.runBreakdown(this.queryHEService, this.isLoadingHE, this.heError, this.heBreakdown);
    else if (this.isHistogram())
      this.runHistogram(this.queryHEService, this.isLoadingHE, this.heError, this.heHistogram);
    else this.runQueries(this.queryHEService, this.isLoadingHE, this.heError, this.heResult);
  }

  onQueryPlaintext(): void {
    if (!this.canQueryPlaintext()) {
      this.plaintextError.set('Plaintext queries are restricted to administrators.');
      return;
    }
    this.pendingRetry = () => this.onQueryPlaintext();
    this.clearPlaintextForQuery();
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

  /**
   * Clears state ahead of an HE query, based on the selected result type. Summary and
   * breakdown stack on top of each other (a fresh one only replaces its own slot,
   * leaving the other visible); histogram is exclusive of both, so a fresh histogram
   * query clears summary and breakdown on both sides, leaving only the histogram.
   */
  private clearHeForQuery(): void {
    if (this.isHistogram()) {
      this.heResult.set(null);
      this.heBreakdown.set(null);
      this.plaintextResult.set(null);
      this.plaintextBreakdown.set(null);
      this.heHistogram.set(null);
    } else if (this.isBreakdown()) {
      this.heBreakdown.set(null);
      this.heHistogram.set(null);
      this.plaintextHistogram.set(null);
    } else {
      this.heResult.set(null);
      this.heHistogram.set(null);
      this.plaintextHistogram.set(null);
    }
  }

  /** Same rules as {@link clearHeForQuery}, mirrored for the plaintext side. */
  private clearPlaintextForQuery(): void {
    if (this.isHistogram()) {
      this.heResult.set(null);
      this.heBreakdown.set(null);
      this.plaintextResult.set(null);
      this.plaintextBreakdown.set(null);
      this.plaintextHistogram.set(null);
    } else if (this.isBreakdown()) {
      this.plaintextBreakdown.set(null);
      this.heHistogram.set(null);
      this.plaintextHistogram.set(null);
    } else {
      this.plaintextResult.set(null);
      this.heHistogram.set(null);
      this.plaintextHistogram.set(null);
    }
  }

  /** The single measurement described by the LOINC inputs, or null if no code was entered. */
  private currentQuery(): MeasurementQuery | null {
    const { loincCode, componentLoincCode } = this.form.getRawValue();
    const code = loincCode?.trim();
    if (!code) return null;
    const componentCode = componentLoincCode?.trim();
    return { loincCode: code, componentLoincCode: componentCode ? componentCode : undefined };
  }

  private buildRequest(
    service: QueryHEService | QueryPlaintextService,
    query: MeasurementQuery,
  ): Observable<QueryResult> {
    const {
      queryType,
      startDate,
      endDate,
      startAge,
      endAge,
      patientSex,
      threshold,
      includeStandardDeviation,
    } = this.form.getRawValue();
    const sex = patientSex ?? undefined;
    const thr = threshold ?? undefined;
    const includeStdDev = includeStandardDeviation ?? false;
    return queryType === 'date'
      ? service.getStatisticsByDateRange(
          query.loincCode,
          query.componentLoincCode,
          startDate ?? undefined,
          endDate ?? undefined,
          sex,
          thr,
          includeStdDev,
        )
      : service.getStatisticsByAgeRange(
          query.loincCode,
          query.componentLoincCode,
          startAge!,
          endAge!,
          sex,
          thr,
          includeStdDev,
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

    const query = this.currentQuery();
    if (!query) return;

    loading.set(true);
    error.set(null);
    result.set(null);

    this.buildRequest(service, query)
      .pipe(
        map((r) => [r]),
        catchError((err: unknown) => {
          this.handleQueryError(err, error);
          return EMPTY;
        }),
        finalize(() => loading.set(false)),
      )
      .subscribe((results) => result.set(results));
  }

  /** Breakdown mode: fan the ordinary average query out over buckets for the queried measurement. */
  private runBreakdown(
    service: QueryHEService | QueryPlaintextService,
    loading: WritableSignal<boolean>,
    error: WritableSignal<string | null>,
    result: WritableSignal<BreakdownResult | null>,
  ): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const query = this.currentQuery();
    if (!query) return;

    const {
      queryType,
      startDate,
      endDate,
      startAge,
      endAge,
      ageBucketSize,
      dateBucketMonths,
      patientSex,
      includeStandardDeviation,
    } = this.form.getRawValue();
    const sex = patientSex ?? undefined;
    const includeStdDev = includeStandardDeviation ?? false;

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
            includeStdDev,
          )
        : service.getBreakdownByAge(
            query.loincCode,
            query.componentLoincCode,
            startAge!,
            endAge!,
            ageBucketSize!,
            sex,
            includeStdDev,
          );

    request$
      .pipe(
        catchError((err: unknown) => {
          this.handleQueryError(err, error);
          return EMPTY;
        }),
        finalize(() => loading.set(false)),
      )
      .subscribe((r) => result.set(r));
  }

  /**
   * Frequency histogram mode: one round trip carrying the whole histogram (unlike the
   * breakdown, which is one query per bucket).
   */
  private runHistogram(
    service: QueryHEService | QueryPlaintextService,
    loading: WritableSignal<boolean>,
    error: WritableSignal<string | null>,
    result: WritableSignal<HistogramResult | null>,
  ): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const query = this.currentQuery();
    if (!query) return;

    const { queryType, startDate, endDate, startAge, endAge, binStart, binWidth, patientSex } =
      this.form.getRawValue();
    const binCount = this.derivedBinCount();
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
        catchError((err: unknown) => {
          this.handleQueryError(err, error);
          return EMPTY;
        }),
        finalize(() => loading.set(false)),
      )
      .subscribe((r) => result.set(r));
  }

  /**
   * Routes a failed query: a 424 means the Client has no LOINC credentials, so open the
   * dialog (remembering which error signal to report to if the user cancels) instead of
   * showing a raw error; anything else is shown normally.
   */
  private handleQueryError(err: unknown, error: WritableSignal<string | null>): void {
    if (err instanceof HttpErrorResponse && err.status === LOINC_CREDENTIALS_REQUIRED_STATUS) {
      this.pendingErrorSignal = error;
      this.showLoincDialog.set(true);
      return;
    }
    error.set(this.extractErrorMessage(err));
  }

  /** Credentials accepted: close the dialog and re-run the query that triggered it. */
  onLoincSaved(): void {
    this.showLoincDialog.set(false);
    const retry = this.pendingRetry;
    this.pendingRetry = null;
    this.pendingErrorSignal = null;
    retry?.();
  }

  /** Dialog dismissed without credentials: leave a note on the originating query. */
  onLoincCancelled(): void {
    this.showLoincDialog.set(false);
    this.pendingErrorSignal?.set('LOINC credentials are required to run this query.');
    this.pendingRetry = null;
    this.pendingErrorSignal = null;
  }

  private extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const problem = err.error as { detail?: string; title?: string } | null;
      return problem?.detail ?? problem?.title ?? `Error ${err.status}: ${err.statusText}`;
    }
    return 'An unexpected error occurred';
  }
}
