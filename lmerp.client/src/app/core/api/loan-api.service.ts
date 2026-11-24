import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';

interface ApiEnvelope<T> { success: boolean; message: string; data: T | null; errors: string[]; }

export interface CustomerDto { id: number; fullName: string; phone: string; email?: string; city?: string; status: string; createdAt: string; loanCount: number; }
export interface CustomerWrite { fullName: string; phone: string; email?: string; city?: string; status: string; }
export interface LoanDto { id: number; applicationNumber: string; customerId: number; customerName: string; product: string; requestedAmount: number; tenureMonths: number; status: string; appliedAt: string; }
export interface LoanWrite { customerId: number; product: string; requestedAmount: number; tenureMonths: number; }
export interface DashboardSummaryDto { customerCount: number; totalLoans: number; activeLoans: number; pendingApplications: number; totalDisbursed: number; outstandingAmount: number; overdueLoans: number; }

@Injectable({ providedIn: 'root' })
export class LoanApiService {
  private readonly http = inject(HttpClient);
  customers(): Observable<CustomerDto[]> { return this.http.get<ApiEnvelope<CustomerDto[]>>('/api/customers').pipe(map(response => response.data ?? [])); }
  createCustomer(customer: CustomerWrite): Observable<CustomerDto> { return this.http.post<ApiEnvelope<CustomerDto>>('/api/customers', customer).pipe(map(response => response.data!)); }
  updateCustomer(id: number, customer: CustomerWrite): Observable<CustomerDto> { return this.http.put<ApiEnvelope<CustomerDto>>(`/api/customers/${id}`, customer).pipe(map(response => response.data!)); }
  deleteCustomer(id: number): Observable<void> { return this.http.delete<ApiEnvelope<boolean>>(`/api/customers/${id}`).pipe(map(() => undefined)); }
  loans(): Observable<LoanDto[]> { return this.http.get<ApiEnvelope<LoanDto[]>>('/api/loans').pipe(map(response => response.data ?? [])); }
  createLoan(loan: LoanWrite): Observable<LoanDto> { return this.http.post<ApiEnvelope<LoanDto>>('/api/loans', loan).pipe(map(response => response.data!)); }
  dashboardSummary(): Observable<DashboardSummaryDto> { return this.http.get<ApiEnvelope<DashboardSummaryDto>>('/api/dashboard/summary').pipe(map(response => response.data!)); }
}
