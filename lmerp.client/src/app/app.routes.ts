import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/login/login.component').then(component => component.LoginComponent), title: 'Sign in · Northstar' },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'dashboard', canActivate: [authGuard], loadComponent: () => import('./features/dashboard/dashboard.component').then(component => component.DashboardComponent), title: 'Overview · Northstar' },
  { path: 'loans/new', canActivate: [authGuard], data: { section: 'loans', create: true }, loadComponent: () => import('./features/admin-page/admin-page.component').then(component => component.AdminPageComponent), title: 'New application · Northstar' },
  ...['customers', 'loans', 'repayments', 'collections', 'reports', 'users', 'settings'].map(section => ({
    path: section,
    canActivate: [authGuard],
    data: { section },
    loadComponent: () => import('./features/admin-page/admin-page.component').then(component => component.AdminPageComponent),
    title: `${section[0].toUpperCase()}${section.slice(1)} · Northstar`
  })),
  { path: '**', redirectTo: 'dashboard' }
];
