import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface LoginResponse {
  token: string;
  username: string;
  role: string;
}

const STORAGE_KEY = 'he-auth';

/**
 * Holds the current session (JWT + role) issued by the Client's /api/auth/login.
 * Persisted to localStorage so a refresh keeps the user signed in. This is the client
 * side of a placeholder auth scheme meant to be swapped for a real provider (e.g. Firebase).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<LoginResponse | null>(this.restore());

  readonly username = computed(() => this.session()?.username ?? null);
  readonly role = computed(() => this.session()?.role ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.session()?.role === 'admin');

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/auth/login', { username, password })
      .pipe(tap((res) => this.persist(res)));
  }

  logout(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  token(): string | null {
    return this.session()?.token ?? null;
  }

  private persist(res: LoginResponse): void {
    this.session.set(res);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(res));
  }

  private restore(): LoginResponse | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as LoginResponse) : null;
    } catch {
      return null;
    }
  }
}
