import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { DashboardSummaryDto, DashboardTransactionDto, LoanApiService, LoanDto } from '../../core/api/loan-api.service';

type DashboardStat = { label: string; value: string; icon: string; tone: string; note: string };

@Component({
  standalone: true,
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  private readonly api = inject(LoanApiService);
  readonly todayLabel = new Intl.DateTimeFormat('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date()).toUpperCase();
  readonly stats = signal<DashboardStat[]>([
    { label: 'Total loans', value: '—', icon: '▤', tone: 'blue', note: 'All active records' },
    { label: 'Total disbursed', value: '—', icon: '↗', tone: 'green', note: 'Disbursed and closed' },
    { label: 'Active loans', value: '—', icon: '▤', tone: 'blue', note: 'Currently disbursed' },
    { label: 'Pending applications', value: '—', icon: '◷', tone: 'orange', note: 'Awaiting review' },
    { label: 'Outstanding', value: '—', icon: '◉', tone: 'violet', note: 'EMI and late fee balances' },
    { label: 'Overdue amount', value: '—', icon: '◷', tone: 'orange', note: 'Past due' }
  ]);
  readonly loans = signal<LoanDto[]>([]);
  readonly transactions = signal<DashboardTransactionDto[]>([]);
  readonly loadError = signal('');
  readonly applications = computed(() => [...this.loans()].sort((a, b) => Date.parse(b.appliedAt) - Date.parse(a.appliedAt)).slice(0, 5));
  readonly portfolio = computed(() => {
    const items = this.loans();
    const total = items.length;
    const count = (predicate: (loan: LoanDto) => boolean) => items.filter(predicate).length;
    return [
      { label: 'Disbursed', count: count(loan => loan.status === 'Disbursed'), tone: 'legend-active' },
      { label: 'Closed', count: count(loan => loan.status === 'Closed'), tone: 'legend-closed' },
      { label: 'In review', count: count(loan => !['Disbursed', 'Closed', 'Rejected'].includes(loan.status)), tone: 'legend-overdue' },
      { label: 'Rejected', count: count(loan => loan.status === 'Rejected'), tone: 'legend-rejected' }
    ].map(item => ({ ...item, percent: total ? Math.round(item.count * 100 / total) : 0 }));
  });
  readonly monthlyApplications = computed(() => {
    const now = new Date();
    const buckets = Array.from({ length: 12 }, (_, index) => {
      const date = new Date(now.getFullYear(), now.getMonth() - 11 + index, 1);
      return { label: new Intl.DateTimeFormat('en-IN', { month: 'short' }).format(date), year: date.getFullYear(), month: date.getMonth(), count: 0 };
    });
    for (const loan of this.loans()) {
      const date = new Date(loan.appliedAt);
      const bucket = buckets.find(item => item.year === date.getFullYear() && item.month === date.getMonth());
      if (bucket) bucket.count++;
    }
    const max = Math.max(1, ...buckets.map(item => item.count));
    return buckets.map(item => ({ ...item, height: Math.max(item.count ? 8 : 2, item.count * 100 / max) }));
  });

  constructor() {
    forkJoin({
      summary: this.api.dashboardSummary().pipe(catchError(() => of(null))),
      loans: this.api.loans().pipe(catchError(() => of(null))),
      transactions: this.api.recentDashboardTransactions().pipe(catchError(() => of(null)))
    }).subscribe(({ summary, loans, transactions }) => {
      if (summary) this.bindSummary(summary);
      if (loans) this.loans.set(loans);
      if (transactions) this.transactions.set(transactions);
      if (!summary || !loans || !transactions) this.loadError.set('Some dashboard data could not be loaded. Check the API connection and refresh.');
    });
  }

  private bindSummary(summary: DashboardSummaryDto): void {
    this.stats.set([
      { label: 'Total loans', value: summary.totalLoans.toLocaleString('en-IN'), icon: '▤', tone: 'blue', note: 'All active records' },
      { label: 'Total disbursed', value: this.currency(summary.totalDisbursed), icon: '↗', tone: 'green', note: 'Disbursed and closed' },
      { label: 'Active loans', value: summary.activeLoans.toLocaleString('en-IN'), icon: '▤', tone: 'blue', note: 'Currently disbursed' },
      { label: 'Pending applications', value: summary.pendingApplications.toLocaleString('en-IN'), icon: '◷', tone: 'orange', note: 'Awaiting review' },
      { label: 'Outstanding', value: this.currency(summary.outstandingAmount), icon: '◉', tone: 'violet', note: 'EMI and late fee balances' },
      { label: 'Overdue amount', value: this.currency(summary.overdueAmount), icon: '◷', tone: 'orange', note: `${summary.overdueLoans} overdue loans` }
    ]);
  }

  currency(amount: number): string {
    const formatter = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 1 });
    if (amount >= 1e7) return `₹ ${formatter.format(amount / 1e7)} Cr`;
    if (amount >= 1e5) return `₹ ${formatter.format(amount / 1e5)} L`;
    return `₹ ${new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(amount)}`;
  }
  initials(value: string): string { return value.split(' ').map(part => part[0] ?? '').slice(0, 2).join('').toUpperCase(); }
  money(value: number): string { return `₹ ${value.toLocaleString('en-IN')}`; }
  date(value: string): string { return new Date(value).toLocaleDateString('en-IN'); }
  time(value: string): string { return new Date(value).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' }); }
  transactionDetail(item: DashboardTransactionDto): string { return `${item.type === 'Disbursement' ? 'Loan disbursement' : 'EMI repayment'} · ${item.applicationNumber}${item.method ? ` · ${item.method}` : ''}`; }
  statusTone(status: string): string {
    if (status === 'Approved' || status === 'Disbursed' || status === 'Closed') return 'approved';
    if (status === 'Rejected') return 'needed';
    return 'review';
  }
}
