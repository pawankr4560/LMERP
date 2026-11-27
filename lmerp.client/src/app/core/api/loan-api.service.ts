import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';

interface ApiEnvelope<T> { success: boolean; message: string; data: T | null; errors: string[]; }

export interface CustomerDto { id: number; fullName: string; phone: string; email?: string; city?: string; status: string; createdAt: string; loanCount: number; rowVersion: string; }
export interface CustomerWrite { fullName: string; phone: string; email?: string; city?: string; status: string; rowVersion?: string; }
export type LoanStatus = 'Under review' | 'Documents needed' | 'Approved' | 'Rejected';
export interface LoanDto { id: number; applicationNumber: string; customerId: number; customerName: string; product: string; requestedAmount: number; tenureMonths: number; interestRate: number; isFlatInterest: boolean; status: string; appliedAt: string; rowVersion: string; actualDisbursedAmount?: number; processingFeePercentage?: number; processingFeeAmount?: number; platformFeePercentage?: number; platformFeeAmount?: number; }
export interface LoanWrite { customerId: number; product: string; requestedAmount: number; tenureMonths: number; interestRate: number; isFlatInterest: boolean; applicationDate: string; }
export interface LoanUpdateWrite { product: string; requestedAmount: number; tenureMonths: number; interestRate: number; isFlatInterest: boolean; status: LoanStatus; rowVersion: string; }
export interface LoanProductDto { id: number; name: string; isActive: boolean; isLateFeeApplicable: boolean; lateFeeRate: number; lateFeeCalculationType: string; gracePeriodDays: number; maximumLateFeeAmount?: number; maximumPenaltyDays?: number; }
export interface LoanProductWrite { name: string; isActive: boolean; isLateFeeApplicable: boolean; lateFeeRate: number; lateFeeCalculationType: string; gracePeriodDays: number; maximumLateFeeAmount?: number; maximumPenaltyDays?: number; }
export interface RepaymentAllocationDto { component: string; amount: number; lateFeeId?: number; }
export interface EmiPaymentDto { paymentId: number; emiScheduleId: number; amount: number; receivedAt: string; method: string; reference?: string; allocations: RepaymentAllocationDto[]; }
export interface LateFeeDto { id: number; penaltyDate: string; baseAmount: number; penaltyRate: number; penaltyAmount: number; status: string; createdAt: string; waiverReason?: string; }
export interface OverdueEmiDto { emiScheduleId: number; loanApplicationId: number; applicationNumber: string; customerName: string; installmentNumber: number; dueDate: string; emiAmount: number; emiOutstanding: number; lateFeesOutstanding: number; totalOutstanding: number; status: string; lateFees: LateFeeDto[]; payments: EmiPaymentDto[]; }
export interface RepaymentSummaryDto { overdueInstallmentCount: number; emiOutstanding: number; lateFeesOutstanding: number; totalOutstanding: number; }
export interface DashboardSummaryDto { customerCount: number; totalLoans: number; activeLoans: number; pendingApplications: number; totalDisbursed: number; outstandingAmount: number; overdueLoans: number; overdueAmount: number; }
export interface DashboardTransactionDto { id: number; customerName: string; applicationNumber: string; type: 'Disbursement' | 'Repayment'; amount: number; occurredAt: string; method?: string; }
export interface ForeclosureChargeRuleDto { id: number; name: string; isEnabled: boolean; isTax: boolean; isServiceFee: boolean; calculationType: 'Percentage' | 'Fixed'; calculationBasis: string; percentage: number; fixedAmount: number; sortOrder: number; }
export type ForeclosureChargeRuleWrite = Omit<ForeclosureChargeRuleDto, 'id'>;
export interface ForeclosureSettingsDto { isForeclosureAllowed: boolean; serviceFeePercentage: number; feeCalculationType: string; updatedAt: string; charges: ForeclosureChargeRuleDto[]; dynamicChargeRulesSupported?: boolean; }
export interface ForeclosureChargeDto { name: string; isTax: boolean; isServiceFee: boolean; calculationType: string; calculationBasis: string; percentage: number; fixedAmount: number; baseAmount: number; amount: number; }
export interface CompanyDocumentSettingsDto { companyName: string; address?: string; phone?: string; email?: string; website?: string; authorizedSignatoryName?: string; authorizedSignatoryDesignation?: string; footerDisclaimer?: string; hasLogo: boolean; hasSignature: boolean; hasStamp: boolean; updatedAt: string; }
export interface LoanDisbursementSettingsDto { processingFeePercentage: number; platformFeePercentage: number; updatedAt: string; updatedBy?: string; }
function normalizeForeclosureSettings(settings: Partial<ForeclosureSettingsDto> | null | undefined): ForeclosureSettingsDto {
  const source = settings ?? {};
  const dynamicChargeRulesSupported = Array.isArray(source.charges);
  const percentage = Number(source.serviceFeePercentage ?? 8);
  const charges = dynamicChargeRulesSupported && source.charges!.length > 0 ? source.charges! : [{ id: 0, name: 'Foreclosure service fee', isEnabled: true, isTax: false, isServiceFee: true, calculationType: 'Percentage' as const, calculationBasis: 'RemainingEmiTotal', percentage, fixedAmount: 0, sortOrder: 0 }];
  return { isForeclosureAllowed: source.isForeclosureAllowed ?? true, serviceFeePercentage: percentage, feeCalculationType: source.feeCalculationType ?? 'RemainingEmiTotal', updatedAt: source.updatedAt ?? '', charges, dynamicChargeRulesSupported };
}
export interface ForeclosureScheduleDto { installmentNumber: number; dueDate: string; emiAmount: number; principalAmount: number; interestAmount: number; paidAmount: number; outstandingAmount: number; status: string; }
export interface ForeclosureExistingPaymentDto { paymentId: number; installmentNumber: number; amount: number; receivedAt: string; method: string; reference?: string; allocations: RepaymentAllocationDto[]; }
export interface ForeclosureLateFeeDto { lateFeeId: number; installmentNumber: number; penaltyDate: string; baseAmount: number; penaltyRate: number; penaltyAmount: number; paidAmount: number; outstandingAmount: number; status: string; waiverReason?: string; }
export interface ForeclosureLoanDto { loanApplicationId: number; loanNumber: string; customerName: string; customerPhone: string; product: string; loanAmount: number; approvedLoanAmount?: number; disbursedAmount?: number; interestRate: number; loanTenureMonths: number; loanStartDate: string; disbursementDate?: string; totalEmiAmount: number; totalInstallments: number; paidInstallments: number; pendingInstallments: number; totalPaidEmiAmount: number; remainingEmiAmount: number; outstandingPrincipal: number; outstandingInterest: number; existingOutstandingLateFees: number; serviceFeePercentage: number; serviceFeeAmount: number; otherCharges: number; adjustments: number; totalForeclosurePayable: number; calculationDate: string; interestBreakdownNote: string; emiSchedule: ForeclosureScheduleDto[]; existingPayments: ForeclosureExistingPaymentDto[]; existingLateFees: ForeclosureLateFeeDto[]; charges: ForeclosureChargeDto[]; }
export interface ForeclosurePaymentDto { id: number; amount: number; receivedAt: string; method: string; reference?: string; collectedBy?: string; }
export interface ForeclosureHistoryDto { id: number; foreclosureNumber: string; foreclosureDate: string; remainingEmiAmount: number; outstandingLateFees: number; serviceFeePercentage: number; serviceFeeAmount: number; totalForeclosureAmount: number; amountPaid: number; balance: number; paymentStatus: string; foreclosureStatus: string; paymentMode: string; transactionReference?: string; remarks?: string; payments: ForeclosurePaymentDto[]; charges: ForeclosureChargeDto[]; }
export interface ForeclosurePaymentWrite { amount: number; paymentMode: string; transactionReference?: string; remarks?: string; }

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
  emiDetails(scheduleId: number): Observable<OverdueEmiDto> { return this.http.get<ApiEnvelope<OverdueEmiDto>>(`/api/repayments/emi/${scheduleId}`).pipe(map(response => response.data!)); }
  repaymentSummary(): Observable<RepaymentSummaryDto> { return this.http.get<ApiEnvelope<RepaymentSummaryDto>>('/api/repayments/summary').pipe(map(response => response.data!)); }
  collectEmiPayment(scheduleId: number, payment: { amount: number; method: string; reference?: string }): Observable<EmiPaymentDto> { return this.http.post<ApiEnvelope<EmiPaymentDto>>(`/api/repayments/emi/${scheduleId}/payments`, payment).pipe(map(response => response.data!)); }
  waiveLateFee(feeId: number, reason: string): Observable<boolean> { return this.http.post<ApiEnvelope<boolean>>(`/api/repayments/late-fees/${feeId}/waive`, { reason }).pipe(map(response => response.data ?? false)); }
  exemptEmiLateFees(scheduleId: number, reason: string): Observable<boolean> { return this.http.post<ApiEnvelope<boolean>>(`/api/repayments/emi/${scheduleId}/exempt-late-fees`, { reason }).pipe(map(response => response.data ?? false)); }
  saveLoanProduct(product: LoanProductWrite): Observable<LoanProductDto> { return this.http.post<ApiEnvelope<LoanProductDto>>('/api/repayments/products', product).pipe(map(response => response.data!)); }
  updateLoanProduct(id: number, product: LoanProductWrite): Observable<LoanProductDto> { return this.http.put<ApiEnvelope<LoanProductDto>>(`/api/repayments/products/${id}`, product).pipe(map(response => response.data!)); }
  createLoan(loan: LoanWrite): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>('/api/loans', loan).pipe(map(response => response.data!)); }
  updateLoan(id: number, loan: LoanUpdateWrite): Observable<LoanDto> { return this.http.put<ApiEnvelope<LoanDto>>(`/api/loans/${id}`, loan).pipe(map(response => response.data!)); }
  disburseLoan(id: number): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>(`/api/loans/${id}/disburse`, {}).pipe(map(response => response.data!)); }
  loanDocument(id: number, type: 'disbursement-advice' | 'emi-statement'): Observable<Blob> { return this.http.get(`/api/documents/loans/${id}/${type}`, { responseType: 'blob' }); }
  companyDocumentSettings(): Observable<CompanyDocumentSettingsDto> { return this.http.get<ApiEnvelope<CompanyDocumentSettingsDto>>('/api/documents/settings').pipe(map(response => response.data!)); }
  updateCompanyDocumentSettings(settings: Omit<CompanyDocumentSettingsDto, 'hasLogo' | 'hasSignature' | 'hasStamp' | 'updatedAt'>, assets: Partial<Record<'logo' | 'signature' | 'stamp', File>>): Observable<CompanyDocumentSettingsDto> {
    const form = new FormData();
    Object.entries(settings).forEach(([key, value]) => { if (value != null) form.append(key, String(value)); });
    (['logo', 'signature', 'stamp'] as const).forEach(key => { const file = assets[key]; if (file) form.append(key, file, file.name); });
    return this.http.put<ApiEnvelope<CompanyDocumentSettingsDto>>('/api/documents/settings', form).pipe(map(response => response.data!));
  }
  loanDisbursementSettings(): Observable<LoanDisbursementSettingsDto> { return this.http.get<ApiEnvelope<LoanDisbursementSettingsDto>>('/api/settings/disbursement-fees').pipe(map(response => response.data!)); }
  updateLoanDisbursementSettings(settings: Pick<LoanDisbursementSettingsDto, 'processingFeePercentage' | 'platformFeePercentage'>): Observable<LoanDisbursementSettingsDto> { return this.http.put<ApiEnvelope<LoanDisbursementSettingsDto>>('/api/settings/disbursement-fees', settings).pipe(map(response => response.data!)); }
  rejectLoan(id: number, reason: string): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>(`/api/loans/${id}/reject`, { reason }).pipe(map(response => response.data!)); }
  deleteLoan(id: number): Observable<void> { return this.http.delete<ApiEnvelope<boolean>>(`/api/loans/${id}`).pipe(map(() => undefined)); }
  dashboardSummary(): Observable<DashboardSummaryDto> { return this.http.get<ApiEnvelope<DashboardSummaryDto>>('/api/dashboard/summary').pipe(map(response => response.data!)); }
  recentDashboardTransactions(): Observable<DashboardTransactionDto[]> { return this.http.get<ApiEnvelope<DashboardTransactionDto[]>>('/api/dashboard/recent-transactions').pipe(map(response => response.data ?? [])); }
  foreclosureSettings(): Observable<ForeclosureSettingsDto> { return this.http.get<ApiEnvelope<Partial<ForeclosureSettingsDto>>>('/api/foreclosures/settings').pipe(map(response => normalizeForeclosureSettings(response.data))); }
  updateForeclosureSettings(settings: Omit<ForeclosureSettingsDto, 'updatedAt' | 'charges' | 'dynamicChargeRulesSupported'> & { charges: ForeclosureChargeRuleWrite[] }): Observable<ForeclosureSettingsDto> { return this.http.put<ApiEnvelope<Partial<ForeclosureSettingsDto>>>('/api/foreclosures/settings', settings).pipe(map(response => normalizeForeclosureSettings(response.data))); }
  foreclosureCalculation(loanNumber: string): Observable<ForeclosureLoanDto> { return this.http.get<ApiEnvelope<ForeclosureLoanDto>>(`/api/foreclosures/loans/${encodeURIComponent(loanNumber)}`).pipe(map(response => response.data!)); }
  foreclosureHistory(loanNumber?: string): Observable<ForeclosureHistoryDto[]> { return this.http.get<ApiEnvelope<ForeclosureHistoryDto[]>>('/api/foreclosures', { params: loanNumber ? { loanNumber } : {} }).pipe(map(response => response.data ?? [])); }
  confirmForeclosure(loanNumber: string, payment: ForeclosurePaymentWrite): Observable<ForeclosureHistoryDto> { return this.http.post<ApiEnvelope<ForeclosureHistoryDto>>(`/api/foreclosures/loans/${encodeURIComponent(loanNumber)}`, payment, { headers: { 'Idempotency-Key': crypto.randomUUID() } }).pipe(map(response => response.data!)); }
  payForeclosure(id: number, payment: ForeclosurePaymentWrite): Observable<ForeclosureHistoryDto> { return this.http.post<ApiEnvelope<ForeclosureHistoryDto>>(`/api/foreclosures/${id}/payments`, payment, { headers: { 'Idempotency-Key': crypto.randomUUID() } }).pipe(map(response => response.data!)); }
}
