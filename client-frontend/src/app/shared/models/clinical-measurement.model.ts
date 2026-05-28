export enum ClinicalMeasurementType {
  BloodPressure = 1,
  HbA1c = 2,
}

export const MEASUREMENT_LABELS: Record<ClinicalMeasurementType, string> = {
  [ClinicalMeasurementType.BloodPressure]: 'Blood Pressure',
  [ClinicalMeasurementType.HbA1c]: 'HbA1c',
};

export const MEASUREMENT_UNITS: Record<ClinicalMeasurementType, string> = {
  [ClinicalMeasurementType.BloodPressure]: 'mmHg',
  [ClinicalMeasurementType.HbA1c]: '%',
};
