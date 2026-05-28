import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ClinicalMeasurementType } from '../models/clinical-measurement.model';

@Injectable({ providedIn: 'root' })
export class StatisticsService {
  private readonly http = inject(HttpClient);

  getAverageByDateRange(
    measurementType: ClinicalMeasurementType,
    startDate?: string,
    endDate?: string,
  ): Observable<number> {
    let params = new HttpParams().set('measurementType', measurementType);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    return this.http.get<number>('/api/statistics/by-date', { params });
  }

  getAverageByAgeRange(
    measurementType: ClinicalMeasurementType,
    startAge?: number,
    endAge?: number,
  ): Observable<number> {
    let params = new HttpParams().set('measurementType', measurementType);
    if (startAge != null) params = params.set('startAge', startAge);
    if (endAge != null) params = params.set('endAge', endAge);
    return this.http.get<number>('/api/statistics/by-age', { params });
  }
}
