import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, tap } from 'rxjs';

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

  constructor() { this.scheduleExpiry(); }

  hasRole(...roles: string[]): boolean {
    return this.sessionIsValid() && roles.some(role => this.roles().includes(role));
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<ApiEnvelope<LoginResponse>>('/api/auth/login', { email, password }).pipe(map(response => response.data!), tap(response => {
      sessionStorage.setItem('northstar.accessToken', response.accessToken);
      sessionStorage.setItem('northstar.displayName', response.fullName);
      sessionStorage.setItem('northstar.roles', JSON.stringify(response.roles));
      sessionStorage.setItem('northstar.expiresAt', response.expiresAt);
      this.displayName.set(response.fullName);
      this.roles.set(response.roles);
      this.isAuthenticated.set(true);
      this.scheduleExpiry();
    }));
  }

  logout(): void {
    this.clearSession();
    void this.router.navigateByUrl('/login');
  }

  private sessionIsValid(): boolean {
    const token = sessionStorage.getItem('northstar.accessToken');
    const expiresAt = sessionStorage.getItem('northstar.expiresAt');
    if (token && expiresAt && Date.parse(expiresAt) > Date.now()) return true;
    if (token || expiresAt) this.clearSession();
    return false;
  }

  private scheduleExpiry(): void {
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    const expiresAt = sessionStorage.getItem('northstar.expiresAt');
    const delay = expiresAt ? Date.parse(expiresAt) - Date.now() : 0;
    if (delay > 0) this.expiryTimer = setTimeout(() => this.logout(), delay);
  }

  private clearSession(): void {
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    sessionStorage.removeItem('northstar.accessToken');
    sessionStorage.removeItem('northstar.expiresAt');
    sessionStorage.removeItem('northstar.displayName');
    sessionStorage.removeItem('northstar.roles');
    this.displayName.set('Workspace administrator');
    this.roles.set([]);
    this.isAuthenticated.set(false);
  }
}
