import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

interface LoginResponse { accessToken: string; expiresAt: string; fullName: string; email: string; roles: string[]; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  readonly isAuthenticated = signal<boolean>(!!sessionStorage.getItem('northstar.accessToken'));
  readonly displayName = signal(sessionStorage.getItem('northstar.displayName') ?? 'Workspace administrator');
  readonly roles = signal<string[]>(JSON.parse(sessionStorage.getItem('northstar.roles') ?? '[]') as string[]);
  readonly accessToken = (): string | null => sessionStorage.getItem('northstar.accessToken');

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(tap(response => {
      sessionStorage.setItem('northstar.accessToken', response.accessToken);
      sessionStorage.setItem('northstar.displayName', response.fullName);
      sessionStorage.setItem('northstar.roles', JSON.stringify(response.roles));
      this.displayName.set(response.fullName);
      this.roles.set(response.roles);
      this.isAuthenticated.set(true);
    }));
  }

  logout(): void {
    sessionStorage.removeItem('northstar.accessToken');
    sessionStorage.removeItem('northstar.displayName');
    sessionStorage.removeItem('northstar.roles');
    this.displayName.set('Workspace administrator');
    this.roles.set([]);
    this.isAuthenticated.set(false);
    void this.router.navigateByUrl('/login');
  }
}
