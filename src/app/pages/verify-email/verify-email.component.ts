import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';

type State = 'verifying' | 'success' | 'error' | 'resend-success';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.css'
})
export class VerifyEmailComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);

  readonly state = signal<State>('verifying');
  readonly errorMessage = signal<string | null>(null);
  readonly resendLoading = signal(false);

  private email = '';

  ngOnInit(): void {
    // Only verify in the browser. Running on the SSR server would consume the
    // single-use token before the browser ever sees the page, causing a 400
    // on the inevitable second call during hydration.
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const email = this.route.snapshot.queryParamMap.get('email') ?? '';
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.email = email;

    if (!email || !token) {
      this.state.set('error');
      this.errorMessage.set('The verification link is missing required parameters.');
      return;
    }

    this.auth.verifyEmail(email, token).subscribe({
      next: () => this.state.set('success'),
      error: (err: { error?: { detail?: string } }) => {
        this.state.set('error');
        this.errorMessage.set(
          err?.error?.detail ?? 'Verification failed. The link may be invalid or expired.'
        );
      }
    });
  }

  resendEmail(): void {
    if (!this.email) return;
    this.resendLoading.set(true);
    this.auth
      .resendVerification(this.email)
      .pipe(finalize(() => this.resendLoading.set(false)))
      .subscribe({
        next: () => this.state.set('resend-success'),
        error: () => {
          this.errorMessage.set('Failed to resend. Please try again or go back to the login page.');
        }
      });
  }
}
