import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LoanApiService } from '../../core/api/loan-api.service';

@Component({
  standalone: true,
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  readonly todayLabel = new Intl.DateTimeFormat('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date()).toUpperCase();
  readonly stats = signal([
    { label: 'Total loans', value: '1,846', change: '+10.4%', icon: '▤', tone: 'blue', note: 'vs. last month' },
    { label: 'Total disbursed', value: '₹ 24.8 Cr', change: '+12.8%', icon: '↗', tone: 'green', note: 'vs. last month' },
    { label: 'Active loans', value: '1,284', change: '+8.2%', icon: '▤', tone: 'blue', note: 'vs. last month' },
    { label: 'Pending applications', value: '42', change: '+6.1%', icon: '◷', tone: 'orange', note: 'awaiting review' },
    { label: 'Outstanding', value: '₹ 18.2 Cr', change: '+4.3%', icon: '◉', tone: 'violet', note: 'vs. last month' },
    { label: 'Overdue amount', value: '₹ 2.46 L', change: '−2.1%', icon: '◷', tone: 'orange', note: 'vs. last month' }
  ]);
  readonly months = [
    { label: 'Jan', value: 49, second: 35 }, { label: 'Feb', value: 62, second: 41 },
    { label: 'Mar', value: 54, second: 38 }, { label: 'Apr', value: 72, second: 49 },
    { label: 'May', value: 66, second: 45 }, { label: 'Jun', value: 85, second: 61 },
    { label: 'Jul', value: 74, second: 54 }, { label: 'Aug', value: 92, second: 66 },
    { label: 'Sep', value: 80, second: 60 }, { label: 'Oct', value: 97, second: 73 },
    { label: 'Nov', value: 88, second: 65 }, { label: 'Dec', value: 100, second: 79 }
  ];
  readonly applications = [
    { initials: 'RK', name: 'Rahul Kumar', id: 'LN-2025-0841', type: 'Personal loan', amount: '₹ 2,50,000', date: 'Today, 10:42 AM', status: 'Under review', tone: 'review', color: 'sand' },
    { initials: 'SP', name: 'Sneha Patel', id: 'LN-2025-0840', type: 'Business loan', amount: '₹ 8,00,000', date: 'Today, 09:18 AM', status: 'Approved', tone: 'approved', color: 'lilac' },
    { initials: 'AM', name: 'Arjun Mehta', id: 'LN-2025-0839', type: 'Vehicle loan', amount: '₹ 5,75,000', date: 'Yesterday', status: 'Documents needed', tone: 'needed', color: 'mint' },
    { initials: 'PS', name: 'Priya Sharma', id: 'LN-2025-0838', type: 'Personal loan', amount: '₹ 1,20,000', date: 'Yesterday', status: 'Under review', tone: 'review', color: 'rose' }
  ];
  readonly transactions = [
    { initials: 'SP', name: 'Sneha Patel', detail: 'Loan disbursement · LN-0832', amount: '+ ₹ 8,00,000', time: '10:24 AM', kind: 'out' },
    { initials: 'VK', name: 'Vikram K.', detail: 'EMI repayment · LN-0718', amount: '+ ₹ 18,500', time: '09:56 AM', kind: 'in' },
    { initials: 'NK', name: 'Neha Kapoor', detail: 'EMI repayment · LN-0654', amount: '+ ₹ 12,200', time: '09:31 AM', kind: 'in' }
  ];

  constructor() {
    inject(LoanApiService).dashboardSummary().subscribe({
      next: summary => this.stats.update(items => items.map(item => {
        if (item.label === 'Total loans') return { ...item, value: summary.totalLoans.toLocaleString('en-IN') };
        if (item.label === 'Pending applications') return { ...item, value: summary.pendingApplications.toLocaleString('en-IN') };
        if (item.label === 'Total disbursed') return { ...item, value: this.currency(summary.totalDisbursed) };
        if (item.label === 'Active loans') return { ...item, value: summary.activeLoans.toLocaleString('en-IN') };
        if (item.label === 'Outstanding') return { ...item, value: this.currency(summary.outstandingAmount) };
        if (item.label === 'Overdue amount') return { ...item, value: this.currency(0), note: `${summary.overdueLoans} overdue loans` };
        return item;
      }))
    });
  }

  private currency(amount: number): string {
    return `₹ ${new Intl.NumberFormat('en-IN', { maximumFractionDigits: 1 }).format(amount / 100000)} L`;
  }
}
