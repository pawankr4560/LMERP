import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './core/auth/auth.service';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated: () => true, displayName: () => 'Alex Morgan', logout: () => undefined } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('renders the admin workspace shell', () => {
    expect(fixture.nativeElement.querySelector('.brand-copy strong')?.textContent).toContain('northstar');
    expect(fixture.nativeElement.querySelectorAll('.nav-link').length).toBe(8);
  });

  it('shows the admin navigation labels', () => {
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Customers');
    expect(text).toContain('Loan applications');
    expect(text).toContain('Settings');
  });
});
