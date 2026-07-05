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

export enum ClinicalMeasurementType {
  BloodPressure = 'BloodPressure',
  HbA1c = 'HbA1c',
}


export interface QueryResult {
  measurementName: string;
  value: number;
  stdDev: number;
  unitOfMeasurement: string;
}
