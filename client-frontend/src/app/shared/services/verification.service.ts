import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ClinicalMeasurementType } from '../models/clinical-measurement.model';

@Injectable({ providedIn: 'root' })
export class VerificationService {
  private readonly http = inject(HttpClient);

  getAverageByDateRange(
    measurementType: ClinicalMeasurementType,
    startDate?: string,
    endDate?: string,
  ): Observable<number> {
    let params = new HttpParams().set('measurementType', measurementType);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    return this.http.get<number>('/api/verification/by-date', { params });
  }
}
