import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface CustomerDto { id: number; fullName: string; phone: string; email?: string; city?: string; status: string; createdAt: string; loanCount: number; }
export interface CustomerWrite { fullName: string; phone: string; email?: string; city?: string; status: string; }
export interface LoanDto { id: number; applicationNumber: string; customerId: number; customerName: string; product: string; requestedAmount: number; tenureMonths: number; status: string; appliedAt: string; }
export interface LoanWrite { customerId: number; product: string; requestedAmount: number; tenureMonths: number; }
export interface DashboardSummaryDto { customerCount: number; totalLoans: number; activeLoans: number; pendingApplications: number; totalDisbursed: number; outstandingAmount: number; overdueLoans: number; }

@Injectable({ providedIn: 'root' })
export class LoanApiService {
  private readonly http = inject(HttpClient);
  customers(): Observable<CustomerDto[]> { return this.http.get<CustomerDto[]>('/api/customers'); }
  createCustomer(customer: CustomerWrite): Observable<CustomerDto> { return this.http.post<CustomerDto>('/api/customers', customer); }
  updateCustomer(id: number, customer: CustomerWrite): Observable<CustomerDto> { return this.http.put<CustomerDto>(`/api/customers/${id}`, customer); }
  deleteCustomer(id: number): Observable<void> { return this.http.delete<void>(`/api/customers/${id}`); }
  loans(): Observable<LoanDto[]> { return this.http.get<LoanDto[]>('/api/loans'); }
  createLoan(loan: LoanWrite): Observable<LoanDto> { return this.http.post<LoanDto>('/api/loans', loan); }
  dashboardSummary(): Observable<DashboardSummaryDto> { return this.http.get<DashboardSummaryDto>('/api/dashboard/summary'); }
}
