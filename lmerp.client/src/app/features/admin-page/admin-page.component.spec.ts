import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoanApiService } from '../../core/api/loan-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { AdminPageComponent } from './admin-page.component';

describe('AdminPageComponent API failures', () => {
  let fixture: ComponentFixture<AdminPageComponent>;
  let api: jasmine.SpyObj<LoanApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<LoanApiService>('LoanApiService', ['customers', 'createCustomer', 'updateCustomer', 'deleteCustomer', 'loans', 'createLoan']);
    api.customers.and.returnValue(of([]));
    api.loans.and.returnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [AdminPageComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { data: of({ section: 'customers' }) } },
        { provide: LoanApiService, useValue: api },
        { provide: AuthService, useValue: { hasRole: () => true } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(AdminPageComponent);
    fixture.detectChanges();
  });

  it('does not add a customer and keeps the form open when the API rejects the request', () => {
    api.createCustomer.and.returnValue(throwError(() => ({ error: { message: 'Customer service is unavailable.', errors: ['Try again later.'] } })));
    const component = fixture.componentInstance;
    component.openDialog();
    component.form.setValue({ fullName: 'Test Customer', phone: '+91 90000 00000', email: 'test@example.com', city: 'Pune', status: 'Active' });

    component.save();

    expect(component.rows()).toHaveSize(0);
    expect(component.showDialog()).toBeTrue();
    expect(component.toast()).toContain('Customer service is unavailable.');
    expect(component.toast()).toContain('Try again later.');
  });

  it('clears an edited customer id after cancel before opening a create form', () => {
    const component = fixture.componentInstance;
    component.openEdit({ id: '17', customer: 'Customer A|CUS-00017', phone: '90000', email: '', city: '', status: 'Active' });
    expect(component.editingId()).toBe(17);

    component.closeDialog();
    component.openDialog();

    expect(component.editingId()).toBeNull();
  });
});
