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
  BloodPressure = 1,
  HbA1c = 2,
}


export interface QueryResult {
  measurementName: string;
  value: number;
  unitOfMeasurement: string;
}
