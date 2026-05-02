import { isPlatformBrowser } from '@angular/common';
import { HttpBackend, HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import type { AuthTokensResponse } from '../api-contract/generated';

const ACCESS_KEY = 'auth_token';
const REFRESH_KEY = 'auth_refresh_token';
const EMAIL_KEY = 'auth_email';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly router = inject(Router);
  private readonly httpBackend = inject(HttpBackend);
  /** Bypasses interceptors — used for refresh to avoid recursion. */
  private readonly rawHttp = new HttpClient(this.httpBackend);

  readonly userEmail = signal<string | null>(null);

  private refreshCall$: Observable<AuthTokensResponse> | null = null;

  constructor() {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    this.userEmail.set(localStorage.getItem(EMAIL_KEY));
  }

  getAccessToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }
    return localStorage.getItem(ACCESS_KEY);
  }

  getRefreshToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }
    return localStorage.getItem(REFRESH_KEY);
  }

  hasValidAccessToken(): boolean {
    const t = this.getAccessToken();
    return !!t && !this.isAccessTokenExpired(t);
  }

  ensureAuthenticated(): Observable<void> {
    if (this.hasValidAccessToken()) {
      return of(void 0);
    }
    const refresh = this.getRefreshToken();
    if (!refresh) {
      return throwError(() => new Error('Not authenticated'));
    }
    return this.refreshSession().pipe(map(() => void 0));
  }

  login(email: string, password: string): Observable<AuthTokensResponse> {
    return this.rawHttp
      .post<AuthTokensResponse>(`${environment.apiBaseUrl}/auth/login`, {
        email: email.trim(),
        password
      })
      .pipe(
        map(res => {
          this.persistSession(res);
          return res;
        })
      );
  }

  /**
   * Shared refresh — concurrent 401s wait on the same in-flight refresh.
   */
  refreshSession(): Observable<AuthTokensResponse> {
    if (!isPlatformBrowser(this.platformId)) {
      return throwError(() => new Error('No browser'));
    }
    const refresh = localStorage.getItem(REFRESH_KEY);
    if (!refresh) {
      return throwError(() => new Error('No refresh token'));
    }

    if (!this.refreshCall$) {
      this.refreshCall$ = this.rawHttp
        .post<AuthTokensResponse>(`${environment.apiBaseUrl}/auth/refresh`, {
          refreshToken: refresh
        })
        .pipe(
          map(res => {
            this.persistSession(res);
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
    const refresh = this.getRefreshToken();
    const access = this.getAccessToken();
    const headers = access
      ? new HttpHeaders({ Authorization: `Bearer ${access}` })
      : undefined;

    return this.rawHttp
      .post(
        `${environment.apiBaseUrl}/auth/logout`,
        { refreshToken: refresh },
        { headers, responseType: 'text' }
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

  private persistSession(res: AuthTokensResponse): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(EMAIL_KEY, res.email);
    this.userEmail.set(res.email);
  }

  clearLocalSession(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(EMAIL_KEY);
    this.userEmail.set(null);
  }

  private isAccessTokenExpired(token: string): boolean {
    try {
      const payload = JSON.parse(atob(token.split('.')[1])) as { exp?: number };
      if (!payload.exp) {
        return true;
      }
      const skewMs = 15_000;
      return payload.exp * 1000 < Date.now() + skewMs;
    } catch {
      return true;
    }
  }
}
