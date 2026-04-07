import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/landing/landing.component').then(m => m.LandingComponent)
  },
  {
    path: 'screenings',
    loadComponent: () =>
      import('./pages/screenings/screenings.component').then(m => m.ScreeningsComponent)
  },
  {
    path: 'upload',
    loadComponent: () =>
      import('./pages/upload/upload.component').then(m => m.UploadComponent)
  },
  {
    path: 'results/:jobId',
    loadComponent: () =>
      import('./pages/results/results.component').then(m => m.ResultsComponent)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
