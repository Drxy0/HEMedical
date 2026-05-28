import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/clinical-query/clinical-query.component').then(
        (m) => m.ClinicalQueryComponent,
      ),
  },
];
