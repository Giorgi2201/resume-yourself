import { isPlatformBrowser } from '@angular/common';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, throwError } from 'rxjs';
import { jwtDecode } from 'jwt-decode';
import { environment } from '../../environments/environment';
import type { AccessTokenResponse } from '../api-contract/generated';

const EMAIL_KEY = 'auth_email';

export interface MessageResponse {
  message: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly router = inject(Router);
  private readonly httpBackend = inject(HttpBackend);
  /** Bypasses interceptors — used for auth calls to avoid recursion. */
  private readonly rawHttp = new HttpClient(this.httpBackend);

  readonly userEmail = signal<string | null>(null);

  /** Access token lives in memory only — never written to localStorage. */
  private accessToken: string | null = null;
  private refreshCall$: Observable<AccessTokenResponse> | null = null;

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;
    this.userEmail.set(localStorage.getItem(EMAIL_KEY));
  }

  // ── Token accessors ──────────────────────────────────────────────────────

  getAccessToken(): string | null {
    return this.accessToken;
  }

  hasValidAccessToken(): boolean {
    const t = this.accessToken;
    return !!t && !this.isAccessTokenExpired(t);
  }

  ensureAuthenticated(): Observable<void> {
    if (this.hasValidAccessToken()) return of(void 0);
    // No in-memory token (e.g. page reload) — attempt silent refresh via httpOnly cookie.
    return this.refreshSession().pipe(map(() => void 0));
  }

  // ── Auth actions ─────────────────────────────────────────────────────────

  register(email: string, password: string, confirmPassword: string): Observable<MessageResponse> {
    return this.rawHttp.post<MessageResponse>(`${environment.apiBaseUrl}/auth/register`, {
      email: email.trim(),
      password,
      confirmPassword
    });
  }

  verifyEmail(email: string, token: string): Observable<MessageResponse> {
    return this.rawHttp.post<MessageResponse>(`${environment.apiBaseUrl}/auth/verify-email`, {
      email,
      token
    });
  }

  resendVerification(email: string): Observable<MessageResponse> {
    return this.rawHttp.post<MessageResponse>(`${environment.apiBaseUrl}/auth/resend-verification`, {
      email: email.trim()
    });
  }

  login(email: string, password: string): Observable<AccessTokenResponse> {
    return this.rawHttp
      .post<AccessTokenResponse>(
        `${environment.apiBaseUrl}/auth/login`,
        { email: email.trim(), password },
        { withCredentials: true }
      )
      .pipe(
        map(res => {
          this.accessToken = res.accessToken;
          this.persistEmail(res.email);
          return res;
        })
      );
  }

  /**
   * Silent token refresh — the browser automatically includes the httpOnly
   * refreshToken cookie. No refresh token is ever readable from JavaScript.
   * Concurrent 401s share a single in-flight request via shareReplay.
   */
  refreshSession(): Observable<AccessTokenResponse> {
    if (!isPlatformBrowser(this.platformId)) {
      return throwError(() => new Error('No browser'));
    }

    if (!this.refreshCall$) {
      this.refreshCall$ = this.rawHttp
        .post<AccessTokenResponse>(
          `${environment.apiBaseUrl}/auth/refresh`,
          {},
          { withCredentials: true }
        )
        .pipe(
          map(res => {
            this.accessToken = res.accessToken;
            this.persistEmail(res.email);
            return res;
          }),
          catchError(err => {
            this.clearLocalSession();
            return throwError(() => err);
          }),
          finalize(() => {
            this.refreshCall$ = null;
          }),
          shareReplay(1)
        );
    }

    return this.refreshCall$;
  }

  logout(): Observable<void> {
    const access = this.accessToken;
    const headers = access ? { Authorization: `Bearer ${access}` } : undefined;

    return this.rawHttp
      .post(
        `${environment.apiBaseUrl}/auth/logout`,
        {},
        { headers, withCredentials: true, responseType: 'text' as const }
      )
      .pipe(
        map(() => void 0),
        catchError(() => of(void 0)),
        finalize(() => {
          this.clearLocalSession();
          void this.router.navigate(['/login']);
        })
      );
  }

  // ── Session helpers ──────────────────────────────────────────────────────

  private persistEmail(email: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    localStorage.setItem(EMAIL_KEY, email);
    this.userEmail.set(email);
  }

  clearLocalSession(): void {
    this.accessToken = null;
    if (!isPlatformBrowser(this.platformId)) return;
    localStorage.removeItem(EMAIL_KEY);
    this.userEmail.set(null);
  }

  private isAccessTokenExpired(token: string): boolean {
    try {
      const payload = jwtDecode<{ exp?: number }>(token);
      if (!payload.exp) return true;
      const skewMs = 15_000;
      return payload.exp * 1000 < Date.now() + skewMs;
    } catch {
      return true;
    }
  }
}
