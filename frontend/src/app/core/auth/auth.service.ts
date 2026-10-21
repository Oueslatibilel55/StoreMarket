import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest, Role, User } from './auth.models';

/**
 * Session state. The access token lives only in memory; the refresh token is an httpOnly
 * cookie the browser sends to /api/auth/* by itself — this code never sees it.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private readonly token = signal<string | null>(null);
  private readonly currentUser = signal<User | null>(null);
  private refreshInFlight: Observable<string | null> | null = null;

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  accessToken(): string | null {
    return this.token();
  }

  hasRole(role: Role): boolean {
    return this.currentUser()?.roles.includes(role) ?? false;
  }

  login(request: LoginRequest): Observable<User> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, request).pipe(
      tap((res) => this.setSession(res)),
      map((res) => res.user),
    );
  }

  register(request: RegisterRequest): Observable<User> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register`, request).pipe(
      tap((res) => this.setSession(res)),
      map((res) => res.user),
    );
  }

  /**
   * Gets a new access token from the refresh cookie. Concurrent callers share one request,
   * so a burst of 401s triggers a single refresh. Emits null when the session is over.
   */
  refresh(): Observable<string | null> {
    this.refreshInFlight ??= this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, null).pipe(
      tap((res) => this.setSession(res)),
      map((res) => res.accessToken),
      catchError(() => {
        this.clearSession();
        return of(null);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** Called once at startup: restores the session if a valid refresh cookie exists. */
  restoreSession(): Observable<unknown> {
    return this.refresh();
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, null).pipe(
      catchError(() => of(undefined)),
      finalize(() => this.clearSession()),
    );
  }

  clearSession(): void {
    this.token.set(null);
    this.currentUser.set(null);
  }

  private setSession(res: AuthResponse): void {
    this.token.set(res.accessToken);
    this.currentUser.set(res.user);
  }
}
