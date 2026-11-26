import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';

interface ApiEnvelope<T> { success: boolean; message: string; data: T | null; errors: string[]; }

export interface CustomerDto { id: number; fullName: string; phone: string; email?: string; city?: string; status: string; createdAt: string; loanCount: number; rowVersion: string; }
export interface CustomerWrite { fullName: string; phone: string; email?: string; city?: string; status: string; rowVersion?: string; }
export type LoanStatus = 'Under review' | 'Documents needed' | 'Approved' | 'Rejected';
export interface LoanDto { id: number; applicationNumber: string; customerId: number; customerName: string; product: string; requestedAmount: number; tenureMonths: number; interestRate: number; isFlatInterest: boolean; status: string; appliedAt: string; rowVersion: string; }
export interface LoanWrite { customerId: number; product: string; requestedAmount: number; tenureMonths: number; interestRate: number; isFlatInterest: boolean; }
export interface LoanUpdateWrite { product: string; requestedAmount: number; tenureMonths: number; interestRate: number; isFlatInterest: boolean; status: LoanStatus; rowVersion: string; }
export interface LoanProductDto { id: number; name: string; isActive: boolean; isLateFeeApplicable: boolean; lateFeeRate: number; lateFeeCalculationType: string; gracePeriodDays: number; maximumLateFeeAmount?: number; maximumPenaltyDays?: number; }
export interface LoanProductWrite { name: string; isActive: boolean; isLateFeeApplicable: boolean; lateFeeRate: number; lateFeeCalculationType: string; gracePeriodDays: number; maximumLateFeeAmount?: number; maximumPenaltyDays?: number; }
export interface RepaymentAllocationDto { component: string; amount: number; lateFeeId?: number; }
export interface EmiPaymentDto { paymentId: number; emiScheduleId: number; amount: number; receivedAt: string; method: string; reference?: string; allocations: RepaymentAllocationDto[]; }
export interface LateFeeDto { id: number; penaltyDate: string; baseAmount: number; penaltyRate: number; penaltyAmount: number; status: string; createdAt: string; waiverReason?: string; }
export interface OverdueEmiDto { emiScheduleId: number; loanApplicationId: number; applicationNumber: string; customerName: string; installmentNumber: number; dueDate: string; emiAmount: number; emiOutstanding: number; lateFeesOutstanding: number; totalOutstanding: number; status: string; lateFees: LateFeeDto[]; payments: EmiPaymentDto[]; }
export interface RepaymentSummaryDto { overdueInstallmentCount: number; emiOutstanding: number; lateFeesOutstanding: number; totalOutstanding: number; }
export interface DashboardSummaryDto { customerCount: number; totalLoans: number; activeLoans: number; pendingApplications: number; totalDisbursed: number; outstandingAmount: number; overdueLoans: number; overdueAmount: number; }

@Injectable({ providedIn: 'root' })
export class LoanApiService {
  private readonly http = inject(HttpClient);
  customers(): Observable<CustomerDto[]> { return this.http.get<ApiEnvelope<CustomerDto[]>>('/api/customers').pipe(map(response => response.data ?? [])); }
  createCustomer(customer: CustomerWrite): Observable<CustomerDto> { return this.http.post<ApiEnvelope<CustomerDto>>('/api/customers', customer).pipe(map(response => response.data!)); }
  updateCustomer(id: number, customer: CustomerWrite): Observable<CustomerDto> { return this.http.put<ApiEnvelope<CustomerDto>>(`/api/customers/${id}`, customer).pipe(map(response => response.data!)); }
  deleteCustomer(id: number): Observable<void> { return this.http.delete<ApiEnvelope<boolean>>(`/api/customers/${id}`).pipe(map(() => undefined)); }
  loans(): Observable<LoanDto[]> { return this.http.get<ApiEnvelope<LoanDto[]>>('/api/loans').pipe(map(response => response.data ?? [])); }
  products(): Observable<LoanProductDto[]> { return this.http.get<ApiEnvelope<LoanProductDto[]>>('/api/repayments/products').pipe(map(response => response.data ?? [])); }
  loanProducts(): Observable<LoanProductDto[]> { return this.products(); }
  overdueEmis(): Observable<OverdueEmiDto[]> { return this.http.get<ApiEnvelope<OverdueEmiDto[]>>('/api/repayments/overdue').pipe(map(response => response.data ?? [])); }
  repaymentSummary(): Observable<RepaymentSummaryDto> { return this.http.get<ApiEnvelope<RepaymentSummaryDto>>('/api/repayments/summary').pipe(map(response => response.data!)); }
  collectEmiPayment(scheduleId: number, payment: { amount: number; method: string; reference?: string }): Observable<EmiPaymentDto> { return this.http.post<ApiEnvelope<EmiPaymentDto>>(`/api/repayments/emi/${scheduleId}/payments`, payment).pipe(map(response => response.data!)); }
  waiveLateFee(feeId: number, reason: string): Observable<boolean> { return this.http.post<ApiEnvelope<boolean>>(`/api/repayments/late-fees/${feeId}/waive`, { reason }).pipe(map(response => response.data ?? false)); }
  exemptEmiLateFees(scheduleId: number, reason: string): Observable<boolean> { return this.http.post<ApiEnvelope<boolean>>(`/api/repayments/emi/${scheduleId}/exempt-late-fees`, { reason }).pipe(map(response => response.data ?? false)); }
  saveLoanProduct(product: LoanProductWrite): Observable<LoanProductDto> { return this.http.post<ApiEnvelope<LoanProductDto>>('/api/repayments/products', product).pipe(map(response => response.data!)); }
  updateLoanProduct(id: number, product: LoanProductWrite): Observable<LoanProductDto> { return this.http.put<ApiEnvelope<LoanProductDto>>(`/api/repayments/products/${id}`, product).pipe(map(response => response.data!)); }
  createLoan(loan: LoanWrite): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>('/api/loans', loan).pipe(map(response => response.data!)); }
  updateLoan(id: number, loan: LoanUpdateWrite): Observable<LoanDto> { return this.http.put<ApiEnvelope<LoanDto>>(`/api/loans/${id}`, loan).pipe(map(response => response.data!)); }
  disburseLoan(id: number): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>(`/api/loans/${id}/disburse`, {}).pipe(map(response => response.data!)); }
  rejectLoan(id: number, reason: string): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>(`/api/loans/${id}/reject`, { reason }).pipe(map(response => response.data!)); }
  deleteLoan(id: number): Observable<void> { return this.http.delete<ApiEnvelope<boolean>>(`/api/loans/${id}`).pipe(map(() => undefined)); }
  dashboardSummary(): Observable<DashboardSummaryDto> { return this.http.get<ApiEnvelope<DashboardSummaryDto>>('/api/dashboard/summary').pipe(map(response => response.data!)); }
}
