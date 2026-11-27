import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ForeclosureExistingPaymentDto, ForeclosureHistoryDto, ForeclosureLoanDto, LoanApiService } from '../../core/api/loan-api.service';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './foreclosure.component.html',
  styleUrl: './foreclosure.component.css'
})
export class ForeclosureComponent {
  private readonly api = inject(LoanApiService);
  readonly loanNumber = signal('');
  readonly loan = signal<ForeclosureLoanDto | null>(null);
  readonly history = signal<ForeclosureHistoryDto[]>([]);
  readonly activeRecord = signal<ForeclosureHistoryDto | null>(null);
  readonly amount = signal(0);
  readonly mode = signal('Cash');
  readonly reference = signal('');
  readonly remarks = signal('');
  readonly busy = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  constructor() { this.loadHistory(); }
  search(): void {
    const number = this.loanNumber().trim();
    if (!number) { this.error.set('Enter a loan number.'); return; }
    this.busy.set(true); this.error.set(''); this.message.set(''); this.loan.set(null); this.activeRecord.set(null);
    this.api.foreclosureCalculation(number).subscribe({ next: loan => { this.loan.set(loan); this.amount.set(loan.totalForeclosurePayable); this.busy.set(false); this.loadHistory(number); }, error: e => { this.error.set(this.errorText(e)); this.busy.set(false); } });
  }
  confirm(): void {
    const loan = this.loan();
    if (!loan || this.amount() <= 0) return;
    if (!window.confirm(`Record ₹${this.amount().toFixed(2)} as a foreclosure payment for ${loan.loanNumber}?`)) return;
    this.busy.set(true); this.error.set('');
    this.api.confirmForeclosure(loan.loanNumber, this.payment()).subscribe({ next: record => this.onRecorded(record), error: e => { this.error.set(this.errorText(e)); this.busy.set(false); } });
  }
  collect(record: ForeclosureHistoryDto): void {
    if (this.amount() <= 0) { this.error.set('Enter a payment amount greater than zero.'); return; }
    if (!window.confirm(`Record ₹${this.amount().toFixed(2)} against ${record.foreclosureNumber}?`)) return;
    this.busy.set(true); this.error.set('');
    this.api.payForeclosure(record.id, this.payment()).subscribe({ next: result => this.onRecorded(result), error: e => { this.error.set(this.errorText(e)); this.busy.set(false); } });
  }
  selectRecord(record: ForeclosureHistoryDto): void { this.activeRecord.set(record); this.amount.set(record.balance); }
  allocationSummary(payment: ForeclosureExistingPaymentDto): string { return payment.allocations.map(allocation => `${allocation.component}: ₹${allocation.amount.toFixed(2)}`).join(', '); }
  private payment() { return { amount: this.amount(), paymentMode: this.mode(), transactionReference: this.reference().trim() || undefined, remarks: this.remarks().trim() || undefined }; }
  private onRecorded(record: ForeclosureHistoryDto): void {
    this.activeRecord.set(record); this.message.set(record.paymentStatus === 'Paid' ? 'Foreclosure fully paid. Loan marked foreclosed.' : 'Partial foreclosure payment recorded. Remaining balance is available for settlement.');
    this.amount.set(record.balance); this.busy.set(false); this.loadHistory(this.loanNumber().trim());
  }
  private loadHistory(number?: string): void { this.api.foreclosureHistory(number).subscribe({ next: records => this.history.set(records), error: e => this.error.set(this.errorText(e)) }); }
  private errorText(error: any): string { return error?.error?.message ?? error?.error?.detail ?? 'The foreclosure request could not be completed.'; }
}
