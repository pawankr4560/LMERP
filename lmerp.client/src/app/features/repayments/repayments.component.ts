import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LoanApiService, LoanProductDto, LoanProductWrite, OverdueEmiDto, RepaymentSummaryDto } from '../../core/api/loan-api.service';

@Component({ standalone: true, imports: [CommonModule, FormsModule], templateUrl: './repayments.component.html', styleUrl: './repayments.component.css' })
export class RepaymentsComponent {
  private readonly api = inject(LoanApiService);
  readonly auth = inject(AuthService);
  readonly rows = signal<OverdueEmiDto[]>([]);
  readonly summary = signal<RepaymentSummaryDto>({ overdueInstallmentCount: 0, emiOutstanding: 0, lateFeesOutstanding: 0, totalOutstanding: 0 });
  readonly products = signal<LoanProductDto[]>([]);
  readonly loading = signal(true);
  readonly message = signal('');
  readonly showProducts = signal(false);
  readonly editingProduct = signal<number | null>(null);
  readonly productForm = signal<LoanProductWrite>({ name: '', isActive: true, isLateFeeApplicable: false, lateFeeRate: 0, lateFeeCalculationType: 'DailyPercentageOfEmi', gracePeriodDays: 0, maximumLateFeeAmount: undefined, maximumPenaltyDays: undefined });
  readonly isAdmin = () => this.auth.roles().includes('Administrator');
  constructor() { this.refresh(); this.loadProducts(); }
  refresh(): void { this.loading.set(true); this.api.overdueEmis().pipe(catchError(() => { this.message.set('Could not load overdue EMI records. Check the API connection.'); return of([]); })).subscribe(rows => { this.rows.set(rows); this.loading.set(false); }); this.api.repaymentSummary().pipe(catchError(() => of(null))).subscribe(summary => { if (summary) this.summary.set(summary); }); }
  loadProducts(): void { this.api.loanProducts().pipe(catchError(() => of([]))).subscribe(products => this.products.set(products)); }
  recordPayment(row: OverdueEmiDto): void {
    const raw = window.prompt(`Outstanding amount ₹${row.totalOutstanding.toLocaleString('en-IN')}. Enter payment amount:`);
    if (raw === null) return;
    const amount = Number(raw);
    if (!Number.isFinite(amount) || amount <= 0) { this.message.set('Enter a valid positive payment amount.'); return; }
    const method = window.prompt('Payment method (UPI, Bank transfer, Cash, etc.):', 'UPI');
    if (!method) return;
    this.api.collectEmiPayment(row.emiScheduleId, { amount, method }).subscribe({ next: () => { this.message.set('Payment recorded and allocated to EMI first, then oldest late fees.'); this.refresh(); }, error: error => this.message.set(error.error?.message ?? 'Could not record payment.') });
  }
  waive(feeId: number): void {
    const reason = window.prompt('Enter the reason for waiving this late fee (minimum 5 characters):');
    if (!reason) return;
    this.api.waiveLateFee(feeId, reason).subscribe({ next: () => { this.message.set('Late fee waived.'); this.refresh(); }, error: error => this.message.set(error.error?.message ?? 'Could not waive this late fee.') });
  }
  exempt(row: OverdueEmiDto): void {
    const reason = window.prompt('Enter the reason for exempting this EMI from future late fees (minimum 5 characters):');
    if (!reason) return;
    this.api.exemptEmiLateFees(row.emiScheduleId, reason).subscribe({ next: () => { this.message.set('EMI exempted from future late fees. Existing charges remain on its history.'); this.refresh(); }, error: error => this.message.set(error.error?.message ?? 'Could not exempt this EMI.') });
  }
  newProduct(): void { this.editingProduct.set(null); this.productForm.set({ name: '', isActive: true, isLateFeeApplicable: false, lateFeeRate: 0, lateFeeCalculationType: 'DailyPercentageOfEmi', gracePeriodDays: 0, maximumLateFeeAmount: undefined, maximumPenaltyDays: undefined }); }
  editProduct(product: LoanProductDto): void { this.editingProduct.set(product.id); this.productForm.set({ name: product.name, isActive: product.isActive, isLateFeeApplicable: product.isLateFeeApplicable, lateFeeRate: product.lateFeeRate, lateFeeCalculationType: product.lateFeeCalculationType, gracePeriodDays: product.gracePeriodDays, maximumLateFeeAmount: product.maximumLateFeeAmount, maximumPenaltyDays: product.maximumPenaltyDays }); }
  toggleProductSettings(): void { this.showProducts.update(value => !value); }
  updateForm(key: keyof LoanProductWrite, value: string | number | boolean | undefined): void { this.productForm.update(form => ({ ...form, [key]: value })); }
  allocationLabel(allocations: { component: string; amount: number }[]): string { return allocations.map(a => `${a.component}: ${this.money(a.amount)}`).join(' · '); }
  saveProduct(): void {
    const form = this.productForm();
    if (!form.name.trim()) { this.message.set('Loan product name is required.'); return; }
    if (form.isLateFeeApplicable && form.lateFeeRate <= 0) { this.message.set('A positive daily rate is required when late fees are enabled.'); return; }
    if (form.isLateFeeApplicable && !form.maximumLateFeeAmount && !form.maximumPenaltyDays) { this.message.set('Set a maximum fee amount or maximum penalty days before enabling fees.'); return; }
    const save$ = this.editingProduct() ? this.api.updateLoanProduct(this.editingProduct()!, form) : this.api.saveLoanProduct(form);
    save$.subscribe({ next: () => { this.message.set('Loan product settings saved. They apply to EMI schedules generated from now on.'); this.loadProducts(); this.newProduct(); }, error: error => this.message.set(error.error?.message ?? 'Could not save product settings.') });
  }
  money(value: number): string { return `₹ ${value.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`; }
}
