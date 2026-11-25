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
export interface LoanProductDto { id: number; name: string; isActive: boolean; }
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
  createLoan(loan: LoanWrite): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>('/api/loans', loan).pipe(map(response => response.data!)); }
  updateLoan(id: number, loan: LoanUpdateWrite): Observable<LoanDto> { return this.http.put<ApiEnvelope<LoanDto>>(`/api/loans/${id}`, loan).pipe(map(response => response.data!)); }
  disburseLoan(id: number): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>(`/api/loans/${id}/disburse`, {}).pipe(map(response => response.data!)); }
  rejectLoan(id: number, reason: string): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>(`/api/loans/${id}/reject`, { reason }).pipe(map(response => response.data!)); }
  deleteLoan(id: number): Observable<void> { return this.http.delete<ApiEnvelope<boolean>>(`/api/loans/${id}`).pipe(map(() => undefined)); }
  dashboardSummary(): Observable<DashboardSummaryDto> { return this.http.get<ApiEnvelope<DashboardSummaryDto>>('/api/dashboard/summary').pipe(map(response => response.data!)); }
}
