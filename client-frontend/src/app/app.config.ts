import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeSr from '@angular/common/locales/sr';

import { routes } from './app.routes';
import { authInterceptor } from './shared/services/auth.interceptor';

// Format numbers the EU way (comma decimal separator, e.g. 80,00). Change this locale
// to switch conventions; only en-US ships by default, so the data must be registered.
registerLocaleData(localeSr);

// Chart.js registration lives in the chart components (breakdown/histogram, lazy route),
// keeping the chart library out of the initial bundle.
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    { provide: LOCALE_ID, useValue: 'sr' },
  ],
};
