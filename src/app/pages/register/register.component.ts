import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css'
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);

  email = '';
  password = '';
  confirmPassword = '';

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successEmail = signal<string | null>(null);

  submit(): void {
    this.errorMessage.set(null);

    if (!this.email.trim() || !this.password || !this.confirmPassword) {
      this.errorMessage.set('Please fill in all fields.');
      return;
    }
    if (this.password !== this.confirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }
    if (this.password.length < 10) {
      this.errorMessage.set('Password must be at least 10 characters.');
      return;
    }

    this.loading.set(true);
    this.auth
      .register(this.email, this.password, this.confirmPassword)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.successEmail.set(this.email.trim());
        },
        error: (err: { error?: { detail?: string } }) => {
          this.errorMessage.set(
            err?.error?.detail ?? 'Registration failed. Please try again.'
          );
        }
      });
  }
}
