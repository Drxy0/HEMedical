import { Routes } from '@angular/router';
import { adminGuard } from './shared/guards/admin.guard';
import { authGuard } from './shared/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'query' },
  {
    path: 'query',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/clinical-query/clinical-query.component').then(
        (m) => m.ClinicalQueryComponent,
      ),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-dashboard.component').then((m) => m.AdminDashboardComponent),
  },
];
