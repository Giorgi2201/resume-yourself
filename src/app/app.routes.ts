import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/landing/landing.component').then(m => m.LandingComponent)
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./pages/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./pages/register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'verify-email',
    loadComponent: () =>
      import('./pages/verify-email/verify-email.component').then(m => m.VerifyEmailComponent)
  },
  {
    path: 'screenings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/screenings/screenings.component').then(m => m.ScreeningsComponent)
  },
  {
    path: 'upload',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/upload/upload.component').then(m => m.UploadComponent)
  },
  {
    path: 'results/:jobId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/results/results.component').then(m => m.ResultsComponent)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
