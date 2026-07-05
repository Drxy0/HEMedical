import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PatientSex, QueryResult } from '../models/clinical-measurement.model';

@Injectable({ providedIn: 'root' })
export class QueryPlaintextService {
  private readonly http = inject(HttpClient);

  getStatisticsByDateRange(
    loincCode: string,
    componentLoincCode?: string,
    startDate?: string,
    endDate?: string,
    sex?: PatientSex,
  ): Observable<QueryResult> {
    let params = new HttpParams().set('loincCode', loincCode);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    if (sex) params = params.set('sex', sex);
    return this.http.get<QueryResult>('/api/verification/by-date', { params });
  }

  getStatisticsByAgeRange(
    loincCode: string,
    componentLoincCode: string | undefined,
    startAge: number,
    endAge: number,
    sex?: PatientSex,
  ): Observable<QueryResult> {
    let params = new HttpParams()
      .set('loincCode', loincCode)
      .set('startAge', startAge)
      .set('endAge', endAge);
    if (componentLoincCode) params = params.set('componentLoincCode', componentLoincCode);
    if (sex) params = params.set('sex', sex);
    return this.http.get<QueryResult>('/api/verification/by-age', { params });
  }
}
