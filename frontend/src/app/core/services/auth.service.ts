import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Role, SsoSession, UserProfile } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  private accessToken = signal<string | null>(null);
  readonly user = signal<UserProfile | null>(null);
  readonly sessionExpiresAt = signal<string | null>(null);
  readonly isAuthenticated = computed(() => !!this.user() && !!this.accessToken());

  token(): string | null {
    return this.accessToken();
  }

  hasRole(...roles: Role[]): boolean {
    const u = this.user();
    return !!u && roles.some((r) => u.roles.includes(r));
  }

  private initStream$?: Observable<boolean>;

  readonly isAdmin = computed(() => this.hasRole('Admin'));

  /** Ensures the initial SSO check runs at most once per page load and shares its completion. */
  ensureInitialized(): Observable<boolean> {
    if (!this.initStream$) {
      this.initStream$ = this.refresh().pipe(
        map(() => true),
        catchError(() => of(false)),
        shareReplay(1)
      );
    }
    return this.initStream$;
  }

  /** Exchange the shared SSO cookie for a fresh access token. 401 => not signed in. */
  refresh(): Observable<SsoSession> {
    return this.http
      .post<SsoSession>(
        `${environment.idpUrl}/sso/session?app=${encodeURIComponent(environment.appCode)}`,
        {},
        { withCredentials: true }
      )
      .pipe(tap((session) => this.setSession(session)));
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${environment.idpUrl}/sso/logout`, {}, { withCredentials: true })
      .pipe(tap({ next: () => this.clearSession(), error: () => this.clearSession() }));
  }

  loginRedirect(returnTo: string = location.href): void {
    location.href = `${environment.loginUrl}?return=${encodeURIComponent(returnTo)}&app=${environment.appCode}`;
  }

  forceClear(): void {
    this.clearSession();
  }

  private setSession(session: SsoSession): void {
    this.accessToken.set(session.accessToken);
    this.user.set(session.user);
    this.sessionExpiresAt.set(session.accessTokenExpiresAt);
  }

  private clearSession(): void {
    this.accessToken.set(null);
    this.user.set(null);
    this.sessionExpiresAt.set(null);
  }
}
