import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { HospitalAdminService } from '../../shared/services/hospital-admin.service';
import { AuthService } from '../../shared/services/auth.service';
import { HospitalAdminView } from '../../shared/models/hospital.model';

@Component({
  selector: 'app-admin-dashboard',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dashboard">
      <header class="dashboard-header">
        <h1>Hospitals — access requests</h1>
        <button type="button" class="refresh" (click)="refresh()" [disabled]="loading()">
          Refresh
        </button>
      </header>

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }

      @if (hospitals().length === 0 && !loading()) {
        <p class="empty">No hospital has registered yet.</p>
      } @else {
        <div class="table-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Address (baseUrl)</th>
                <th scope="col">Status</th>
                <th scope="col">Active</th>
                <th scope="col">Last seen</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (h of hospitals(); track h.baseUrl) {
                <tr>
                  <td>{{ h.name }}</td>
                  <td class="url">{{ h.baseUrl }}</td>
                  <td>
                    <span class="badge" [class]="'badge-' + h.status.toLowerCase()">{{
                      h.status
                    }}</span>
                  </td>
                  <td>{{ h.isActive ? 'Yes' : 'No' }}</td>
                  <td>{{ h.lastSeenUtc | date: 'short' }}</td>
                  <td class="actions">
                    @if (h.status !== 'Approved') {
                      <button type="button" class="approve" (click)="approve(h)" [disabled]="busy()">
                        Approve
                      </button>
                    }
                    @if (h.status !== 'Blocked') {
                      <button type="button" class="block" (click)="block(h)" [disabled]="busy()">
                        Block
                      </button>
                    }
                    <button type="button" class="remove" (click)="remove(h)" [disabled]="busy()">
                      Remove
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styles: `
    .dashboard {
      max-width: 60rem;
      margin: 2rem auto;
      padding: 0 1rem;
    }
    .dashboard-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }
    .table-scroll {
      overflow-x: auto;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      margin-top: 1rem;
    }
    th,
    td {
      text-align: left;
      padding: 0.5rem 0.75rem;
      border-bottom: 1px solid #e5e7eb;
      font-size: 0.9rem;
    }
    .url {
      font-family: monospace;
    }
    .badge {
      display: inline-block;
      padding: 0.15rem 0.5rem;
      border-radius: 0.75rem;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .badge-pending {
      background: #fef3c7;
      color: #92400e;
    }
    .badge-approved {
      background: #dcfce7;
      color: #166534;
    }
    .badge-blocked {
      background: #fee2e2;
      color: #991b1b;
    }
    .actions {
      display: flex;
      gap: 0.5rem;
    }
    button {
      padding: 0.35rem 0.75rem;
      border: none;
      border-radius: 0.375rem;
      font-size: 0.85rem;
      font-weight: 500;
      cursor: pointer;
      color: #fff;
    }
    button:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }
    .approve {
      background: #16a34a;
    }
    .block {
      background: #dc2626;
    }
    .remove {
      background: #6b7280;
    }
    .refresh {
      background: #2563eb;
    }
    .error {
      color: #b91c1c;
    }
    .empty {
      color: #6b7280;
      margin-top: 1rem;
    }
  `,
})
export class AdminDashboardComponent implements OnInit {
  private readonly admin = inject(HospitalAdminService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly hospitals = signal<HospitalAdminView[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.admin.list().subscribe({
      next: (list) => {
        this.hospitals.set(list);
        this.loading.set(false);
      },
      error: (err: unknown) => this.handleError(err),
    });
  }

  approve(hospital: HospitalAdminView): void {
    this.busy.set(true);
    this.admin.approve(hospital.baseUrl).subscribe({
      next: () => this.reloadAfterAction(),
      error: (err: unknown) => this.handleError(err),
    });
  }

  block(hospital: HospitalAdminView): void {
    this.busy.set(true);
    this.admin.block(hospital.baseUrl).subscribe({
      next: () => this.reloadAfterAction(),
      error: (err: unknown) => this.handleError(err),
    });
  }

  /** Permanent (unlike Block): confirm before deleting the registry entry outright. */
  remove(hospital: HospitalAdminView): void {
    if (!confirm(`Remove "${hospital.name}" (${hospital.baseUrl}) from the registry? This cannot be undone.`))
      return;
    this.busy.set(true);
    this.admin.remove(hospital.baseUrl).subscribe({
      next: () => this.reloadAfterAction(),
      error: (err: unknown) => this.handleError(err),
    });
  }

  private reloadAfterAction(): void {
    this.busy.set(false);
    this.refresh();
  }

  private handleError(err: unknown): void {
    this.loading.set(false);
    this.busy.set(false);
    // A 401 means the admin session expired or is missing — send them back to login.
    if (typeof err === 'object' && err !== null && 'status' in err && err.status === 401) {
      this.auth.logout();
      this.router.navigate(['/login']);
      return;
    }
    this.error.set('Action failed. Please try again.');
  }
}
