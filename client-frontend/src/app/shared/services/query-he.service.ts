import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  BreakdownResult,
  HistogramResult,
  PatientSex,
  QueryResult,
} from '../models/clinical-measurement.model';

@Injectable({ providedIn: 'root' })
export class QueryHEService {
  private readonly http = inject(HttpClient);

  getStatisticsByDateRange(
    loincCode: string,
    componentLoincCode?: string,
    startDate?: string,
    endDate?: string,
    sex?: PatientSex,
    threshold?: number,
    includeStandardDeviation = false,
  ): Observable<QueryResult> {
    let params = new HttpParams().set('loincCode', loincCode);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    if (sex) params = params.set('sex', sex);
    if (threshold != null) params = params.set('threshold', threshold);
    // Opt-in (off by default): only sent when requested; an absent param means false server-side.
    if (includeStandardDeviation) params = params.set('includeStandardDeviation', true);
    return this.http.get<QueryResult>('/api/statistics/by-date', { params });
  }

  getStatisticsByAgeRange(
    loincCode: string,
    componentLoincCode: string | undefined,
    startAge: number,
    endAge: number,
    sex?: PatientSex,
    threshold?: number,
    includeStandardDeviation = false,
  ): Observable<QueryResult> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('startAge', startAge)
      .set('endAge', endAge);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (sex) params = params.set('sex', sex);
    if (threshold != null) params = params.set('threshold', threshold);
    if (!includeStandardDeviation) params = params.set('includeStandardDeviation', false);
    return this.http.get<QueryResult>('/api/statistics/by-age', { params });
  }

  getBreakdownByAge(
    loincCode: string,
    componentLoincCode: string | undefined,
    startAge: number,
    endAge: number,
    bucketSize: number,
    sex?: PatientSex,
    includeStandardDeviation = false,
  ): Observable<BreakdownResult> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('startAge', startAge)
      .set('endAge', endAge)
      .set('bucketSize', bucketSize);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (sex) params = params.set('sex', sex);
    if (includeStandardDeviation) params = params.set('includeStandardDeviation', true);
    return this.http.get<BreakdownResult>('/api/statistics/breakdown-by-age', { params });
  }

  getBreakdownByDate(
    loincCode: string,
    componentLoincCode: string | undefined,
    startDate: string,
    endDate: string,
    bucketMonths: number,
    sex?: PatientSex,
    includeStandardDeviation = false,
  ): Observable<BreakdownResult> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('startDate', startDate)
      .set('endDate', endDate)
      .set('bucketMonths', bucketMonths);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (sex) params = params.set('sex', sex);
    if (includeStandardDeviation) params = params.set('includeStandardDeviation', true);
    return this.http.get<BreakdownResult>('/api/statistics/breakdown-by-date', { params });
  }
  getHistogramByDateRange(
    loincCode: string,
    componentLoincCode: string | undefined,
    startDate: string | undefined,
    endDate: string | undefined,
    sex: PatientSex | undefined,
    binStart: number,
    binWidth: number,
    binCount: number,
  ): Observable<HistogramResult> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('binStart', binStart)
      .set('binWidth', binWidth)
      .set('binCount', binCount);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    if (sex) params = params.set('sex', sex);
    return this.http.get<HistogramResult>('/api/statistics/histogram-by-date', { params });
  }

  getHistogramByAgeRange(
    loincCode: string,
    componentLoincCode: string | undefined,
    startAge: number,
    endAge: number,
    sex: PatientSex | undefined,
    binStart: number,
    binWidth: number,
    binCount: number,
  ): Observable<HistogramResult> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('startAge', startAge)
      .set('endAge', endAge)
      .set('binStart', binStart)
      .set('binWidth', binWidth)
      .set('binCount', binCount);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (sex) params = params.set('sex', sex);
    return this.http.get<HistogramResult>('/api/statistics/histogram-by-age', { params });
  }
}
