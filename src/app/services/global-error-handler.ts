import { ErrorHandler, Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private router = inject(Router);

  handleError(error: unknown): void {
    const err = error as { status?: number; message?: string };

    console.error('Unhandled error:', error);

    if (err?.status === 401) {
      this.router.navigate(['/login']);
      return;
    }

    if (err?.status === 403) {
      this.router.navigate(['/']);
      return;
    }

    if (err?.status && err.status >= 500) {
      this.router.navigate(['/error']);
      return;
    }
  }
}
