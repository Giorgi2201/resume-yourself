import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  email = '';
  password = '';

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showResend = signal(false);
  readonly resendLoading = signal(false);
  readonly resendSuccess = signal(false);

  submit(): void {
    this.errorMessage.set(null);
    this.showResend.set(false);
    this.resendSuccess.set(false);

    if (!this.email.trim() || !this.password) {
      this.errorMessage.set('Enter your email and password.');
      return;
    }

    this.loading.set(true);
    this.auth
      .login(this.email, this.password)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/screenings';
          void this.router.navigateByUrl(returnUrl);
        },
        error: (err: { error?: { detail?: string }; status?: number }) => {
          const detail = err?.error?.detail ?? '';
          if (detail.toLowerCase().includes('not verified')) {
            this.errorMessage.set(detail);
            this.showResend.set(true);
          } else {
            this.errorMessage.set(detail || 'Sign-in failed. Check your credentials and try again.');
          }
        }
      });
  }

  resendVerification(): void {
    if (!this.email.trim()) return;
    this.resendLoading.set(true);
    this.auth
      .resendVerification(this.email)
      .pipe(finalize(() => this.resendLoading.set(false)))
      .subscribe({
        next: () => {
          this.resendSuccess.set(true);
          this.showResend.set(false);
        },
        error: () => {
          this.errorMessage.set('Could not resend the email. Please try again later.');
        }
      });
  }
}
