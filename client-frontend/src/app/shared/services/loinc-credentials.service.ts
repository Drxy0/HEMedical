import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface LoincStatus {
  configured: boolean;
}

/**
 * Talks to the Client's /api/loinc endpoints, which hold the LOINC terminology-server
 * account used to verify measurement codes. When a deployment starts without those
 * credentials, queries fail with 424 and the UI collects them here.
 */
@Injectable({ providedIn: 'root' })
export class LoincCredentialsService {
  private readonly http = inject(HttpClient);

  /** null = not yet checked; otherwise whether the Client has LOINC credentials. */
  readonly configured = signal<boolean | null>(null);

  refreshStatus(): Observable<LoincStatus> {
    return this.http
      .get<LoincStatus>('/api/loinc/status')
      .pipe(tap((status) => this.configured.set(status.configured)));
  }

  /** Sends candidate credentials to be validated against loinc.org and stored on success. */
  save(username: string, password: string): Observable<LoincStatus> {
    return this.http
      .post<LoincStatus>('/api/loinc/credentials', { username, password })
      .pipe(tap((status) => this.configured.set(status.configured)));
  }
}
