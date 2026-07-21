import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { HospitalAdminView } from '../models/hospital.model';

/** Admin-only governance of hospital data sources, via the Client's /api/admin/hospitals. */
@Injectable({ providedIn: 'root' })
export class HospitalAdminService {
  private readonly http = inject(HttpClient);

  list(): Observable<HospitalAdminView[]> {
    return this.http.get<HospitalAdminView[]>('/api/admin/hospitals');
  }

  approve(baseUrl: string): Observable<void> {
    return this.http.post<void>('/api/admin/hospitals/approve', { baseUrl });
  }

  block(baseUrl: string): Observable<void> {
    return this.http.post<void>('/api/admin/hospitals/block', { baseUrl });
  }

  remove(baseUrl: string): Observable<void> {
    return this.http.post<void>('/api/admin/hospitals/remove', { baseUrl });
  }
}
