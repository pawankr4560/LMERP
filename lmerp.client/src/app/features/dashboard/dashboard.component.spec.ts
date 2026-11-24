import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();
    fixture = TestBed.createComponent(DashboardComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders portfolio cards and requests the live dashboard summary', () => {
    const req = httpMock.expectOne('/api/dashboard/summary');
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, message: 'ok', data: { customerCount: 3, totalLoans: 3, activeLoans: 1, pendingApplications: 2, totalDisbursed: 800000, outstandingAmount: 1050000, overdueLoans: 0 }, errors: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Disbursement overview');
    expect(fixture.nativeElement.textContent).toContain('10.5 L');
  });
});
