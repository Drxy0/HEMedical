import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ClinicalMeasurementType, PatientSex, QueryResult } from '../models/clinical-measurement.model';

@Injectable({ providedIn: 'root' })
export class QueryHEService {
  private readonly http = inject(HttpClient);

  getAverageByDateRange(
    measurementType: ClinicalMeasurementType,
    startDate?: string,
    endDate?: string,
    sex?: PatientSex,
  ): Observable<QueryResult[]> {
    let params = new HttpParams().set('measurementType', measurementType);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    if (sex) params = params.set('sex', sex);
    return this.http.get<QueryResult[]>('/api/statistics/by-date', { params });
  }

  getAverageByAgeRange(
    measurementType: ClinicalMeasurementType,
    startAge?: number,
    endAge?: number,
    sex?: PatientSex,
  ): Observable<QueryResult[]> {
    let params = new HttpParams().set('measurementType', measurementType);
    if (startAge != null) params = params.set('startAge', startAge);
    if (endAge != null) params = params.set('endAge', endAge);
    if (sex) params = params.set('sex', sex);
    return this.http.get<QueryResult[]>('/api/statistics/by-age', { params });
  }

  getAverageByLoincDateRange(
    loincCode: string,
    startDate?: string,
    endDate?: string,
    sex?: PatientSex,
  ): Observable<QueryResult[]> {
    let params = new HttpParams().set('loincCode', loincCode);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    if (sex) params = params.set('sex', sex);
    return this.http.get<QueryResult[]>('/api/statistics/by-loinc', { params });
  }

  getAverageByLoincAgeRange(
    loincCode: string,
    startAge: number,
    endAge: number,
    sex?: PatientSex,
  ): Observable<QueryResult[]> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('startAge', startAge)
      .set('endAge', endAge);
    if (sex) params = params.set('sex', sex);
    return this.http.get<QueryResult[]>('/api/statistics/by-loinc-age', { params });
  }
}
