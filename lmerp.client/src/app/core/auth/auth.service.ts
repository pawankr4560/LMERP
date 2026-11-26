import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, map, Observable, of, shareReplay, tap, throwError } from 'rxjs';

interface LoginResponse { accessToken: string; expiresAt: string; fullName: string; email: string; roles: string[]; }
interface ApiEnvelope<T> { success: boolean; message: string; data: T | null; errors: string[]; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  readonly isAuthenticated = signal<boolean>(this.sessionIsValid());
  readonly displayName = signal(sessionStorage.getItem('northstar.displayName') ?? 'Workspace administrator');
  readonly roles = signal<string[]>(JSON.parse(sessionStorage.getItem('northstar.roles') ?? '[]') as string[]);
  readonly accessToken = (): string | null => this.sessionIsValid() ? sessionStorage.getItem('northstar.accessToken') : null;
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;
  private refreshRequest?: Observable<LoginResponse>;

  constructor() { this.scheduleRefresh(); }

  hasRole(...roles: string[]): boolean {
    if (!this.sessionIsValid()) return false;
    const assignedRoles = new Set(this.roles());
    if (assignedRoles.has('Admin')) assignedRoles.add('Administrator');
    if (assignedRoles.has('Manager')) assignedRoles.add('CollectionsManager');
    return roles.some(role => assignedRoles.has(role));
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<ApiEnvelope<LoginResponse>>('/api/auth/login', { email, password }, { withCredentials: true }).pipe(
      map(response => response.data!),
      tap(response => this.applySession(response))
    );
  }

  refreshAccessToken(): Observable<LoginResponse> {
    if (this.refreshRequest) return this.refreshRequest;
    this.refreshRequest = this.http.post<ApiEnvelope<LoginResponse>>('/api/auth/refresh', {}, { withCredentials: true }).pipe(
      map(response => response.data!),
      tap(response => this.applySession(response)),
      catchError(error => { this.clearSession(); return throwError(() => error); }),
      finalize(() => { this.refreshRequest = undefined; }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    return this.refreshRequest;
  }

  ensureAuthenticated(): Observable<boolean> {
    if (this.accessToken()) return of(true);
    return this.refreshAccessToken().pipe(map(() => true), catchError(() => of(false)));
  }

  logout(): void {
    this.http.post('/api/auth/logout', {}, { withCredentials: true }).pipe(catchError(() => of(null))).subscribe();
    this.expireSession();
  }

  expireSession(): void {
    this.clearSession();
    void this.router.navigateByUrl('/login');
  }

  private applySession(response: LoginResponse): void {
    sessionStorage.setItem('northstar.accessToken', response.accessToken);
    sessionStorage.setItem('northstar.displayName', response.fullName);
    sessionStorage.setItem('northstar.roles', JSON.stringify(response.roles));
    sessionStorage.setItem('northstar.expiresAt', response.expiresAt);
    this.displayName.set(response.fullName);
    this.roles.set(response.roles);
    this.isAuthenticated.set(true);
    this.scheduleRefresh();
  }

  private sessionIsValid(): boolean {
    const token = sessionStorage.getItem('northstar.accessToken');
    const expiresAt = sessionStorage.getItem('northstar.expiresAt');
    if (token && expiresAt && Date.parse(expiresAt) > Date.now()) return true;
    if (token || expiresAt) this.clearSession();
    return false;
  }

  private scheduleRefresh(): void {
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    const expiresAt = sessionStorage.getItem('northstar.expiresAt');
    if (!expiresAt || !sessionStorage.getItem('northstar.accessToken')) return;
    const remaining = Date.parse(expiresAt) - Date.now();
    const refreshLead = Math.min(60_000, Math.max(1, Math.floor(remaining / 2)));
    const delay = Math.max(0, remaining - refreshLead);
    this.expiryTimer = setTimeout(() => this.refreshAccessToken().subscribe({ error: () => this.expireSession() }), delay);
  }

  private clearSession(): void {
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    this.expiryTimer = undefined;
    sessionStorage.removeItem('northstar.accessToken');
    sessionStorage.removeItem('northstar.expiresAt');
    sessionStorage.removeItem('northstar.displayName');
    sessionStorage.removeItem('northstar.roles');
    this.displayName.set('Workspace administrator');
    this.roles.set([]);
    this.isAuthenticated.set(false);
  }
}
