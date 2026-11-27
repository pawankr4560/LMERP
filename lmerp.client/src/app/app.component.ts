import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';

interface NavItem { label: string; path: string; icon: string; }

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  readonly auth = inject(AuthService);
  readonly userInitials = computed(() => this.auth.displayName().split(' ').map(part => part[0] ?? '').slice(0, 2).join('').toUpperCase());
  readonly currentPage = signal('Overview');
  readonly sidebarOpen = signal(false);
  readonly navGroups: { title: string; items: NavItem[] }[] = [
    { title: 'WORKSPACE', items: [
      { label: 'Overview', path: '/dashboard', icon: '◫' },
      { label: 'Customers', path: '/customers', icon: '♙' },
      { label: 'Loan applications', path: '/loans', icon: '▤' },
      { label: 'Repayments', path: '/repayments', icon: '↻' },
      { label: 'Collections', path: '/collections', icon: '◷' },
      { label: 'Loan Foreclosure', path: '/foreclosure', icon: '₹' }
    ] },
    { title: 'INSIGHTS & ADMIN', items: [
      { label: 'Reports', path: '/reports', icon: '▥' },
      { label: 'Users & roles', path: '/users', icon: '♧' },
      { label: 'Settings', path: '/settings', icon: '⚙' }
    ] }
  ];

  constructor() {
    inject(Router).events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)).subscribe(event => {
      const lastSegment = event.urlAfterRedirects.split('?')[0].split('/').filter(Boolean).at(-1) ?? 'dashboard';
      this.currentPage.set(lastSegment === 'new' ? 'New application' : lastSegment.replaceAll('-', ' ').replace(/\b\w/g, letter => letter.toUpperCase()));
    });
  }

  closeSidebar(): void { this.sidebarOpen.set(false); }
}
