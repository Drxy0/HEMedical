export enum PatientSex {
  Male = 'Male',
  Female = 'Female',
  Other = 'Other',
}

export const SEX_LABELS: Record<PatientSex, string> = {
  [PatientSex.Male]: 'Male',
  [PatientSex.Female]: 'Female',
  [PatientSex.Other]: 'Other',
};

/**
 * A single backend query: a LOINC code, plus an optional component code for
 * values recorded inside panel observations (e.g. systolic inside the BP panel).
 * `label`/`unit` are display overrides; when absent the backend-provided
 * LOINC display name is shown.
 */
export interface MeasurementQuery {
  loincCode: string;
  componentLoincCode?: string;
  label?: string;
  unit?: string;
}

/** A dropdown preset. One preset can fan out into several queries (e.g. blood pressure). */
export interface MeasurementPreset {
  id: string;
  label: string;
  queries: MeasurementQuery[];
}

export const MEASUREMENT_PRESETS: MeasurementPreset[] = [
  {
    id: 'blood-pressure',
    label: 'Blood Pressure',
    queries: [
      {
        loincCode: '85354-9',
        componentLoincCode: '8480-6',
        label: 'Systolic Blood Pressure',
        unit: 'mm[Hg]',
      },
      {
        loincCode: '85354-9',
        componentLoincCode: '8462-4',
        label: 'Diastolic Blood Pressure',
        unit: 'mm[Hg]',
      },
    ],
  },
  {
    id: 'hba1c',
    label: 'HbA1c',
    queries: [{ loincCode: '4548-4', label: 'HbA1c', unit: '%' }],
  },
];

export interface QueryResult {
  measurementName: string;
  value: number;
  stdDev: number;
  unitOfMeasurement: string;
  sum: number;
  count: number;
  threshold: number | null;
  countAboveThreshold: number | null;
  prevalenceAboveThreshold: number | null;
}

/** One bar of a breakdown: the average (± σ) for one age group or time period. */
export interface BreakdownBucket {
  label: string;
  average: number;
  stdDev: number;
  hasData: boolean;
}

export interface BreakdownResult {
  measurementName: string;
  unitOfMeasurement: string;
  buckets: BreakdownBucket[];
}

/** One bar of a frequency histogram: how many patients fall in one value range. */
export interface HistogramBin {
  label: string;
  from: number;
  to: number;
  count: number;
}

/**
 * A frequency histogram: patient counts per value range. Values outside the requested
 * bins are reported as the below/above edge counts, so bins + edges = full cohort.
 */
export interface HistogramResult {
  measurementName: string;
  unitOfMeasurement: string;
  bins: HistogramBin[];
  belowRangeCount: number;
  aboveRangeCount: number;
}
