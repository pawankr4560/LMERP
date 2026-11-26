import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormRecord, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CustomerDto, LoanApiService, LoanDto, LoanStatus, OverdueEmiDto } from '../../core/api/loan-api.service';
import { AuthService } from '../../core/auth/auth.service';

interface PageRow { [key: string]: string; }
interface PageDefinition { title: string; description: string; action: string; columns: { key: string; label: string }[]; rows: PageRow[]; fields: { key: string; label: string; type?: string; options?: string[] }[]; }

const definitions: Record<string, PageDefinition> = {
  customers: {
    title: 'Customers', description: 'Manage customer profiles, KYC records, and loan relationships.', action: 'Add customer',
    columns: [{key:'customer',label:'CUSTOMER'},{key:'phone',label:'CONTACT'},{key:'loans',label:'LOANS'},{key:'joined',label:'JOINED'},{key:'status',label:'STATUS'}],
    rows: [
      {customer:'Rahul Kumar|CUS-02481',phone:'+91 98765 43210',loans:'2 active',joined:'18 Nov 2025',status:'Active'},
      {customer:'Sneha Patel|CUS-02480',phone:'+91 98201 34678',loans:'1 active',joined:'18 Nov 2025',status:'Active'},
      {customer:'Arjun Mehta|CUS-02479',phone:'+91 99876 12345',loans:'1 pending',joined:'17 Nov 2025',status:'Review'},
      {customer:'Priya Sharma|CUS-02478',phone:'+91 97654 09876',loans:'3 active',joined:'17 Nov 2025',status:'Active'},
      {customer:'Vikram Khanna|CUS-02477',phone:'+91 98989 01234',loans:'1 closed',joined:'16 Nov 2025',status:'Inactive'},
      {customer:'Neha Kapoor|CUS-02476',phone:'+91 98111 22334',loans:'2 active',joined:'16 Nov 2025',status:'Active'}
    ], fields:[{key:'fullName',label:'Full name'},{key:'phone',label:'Phone number',type:'tel'},{key:'email',label:'Email address',type:'email'},{key:'city',label:'City'},{key:'status',label:'Status',type:'select',options:['Active','Inactive']}]
  },
  loans: {
    title:'Loan applications', description:'Review applications, manage loan products, and track disbursements.', action:'New application',
    columns:[{key:'application',label:'APPLICATION'},{key:'product',label:'PRODUCT'},{key:'amount',label:'REQUESTED'},{key:'date',label:'APPLIED'},{key:'status',label:'STATUS'}],
    rows:[
      {application:'Rahul Kumar|LN-2025-0841',product:'Personal loan',amount:'₹ 2,50,000',date:'Today, 10:42 AM',status:'Under review'},
      {application:'Sneha Patel|LN-2025-0840',product:'Business loan',amount:'₹ 8,00,000',date:'Today, 09:18 AM',status:'Approved'},
      {application:'Arjun Mehta|LN-2025-0839',product:'Vehicle loan',amount:'₹ 5,75,000',date:'Yesterday',status:'Documents needed'},
      {application:'Priya Sharma|LN-2025-0838',product:'Personal loan',amount:'₹ 1,20,000',date:'Yesterday',status:'Under review'},
      {application:'Vikram Khanna|LN-2025-0837',product:'Home loan',amount:'₹ 32,00,000',date:'17 Nov 2025',status:'Disbursed'},
      {application:'Neha Kapoor|LN-2025-0836',product:'Personal loan',amount:'₹ 3,00,000',date:'17 Nov 2025',status:'Rejected'}
    ], fields:[{key:'customerId',label:'Customer',type:'select'},{key:'product',label:'Loan product',type:'select',options:['Personal loan','Business loan','Vehicle loan','Home loan']},{key:'requestedAmount',label:'Requested amount',type:'number'},{key:'tenureMonths',label:'Tenure (months)',type:'number'},{key:'interestRate',label:'Interest rate (% per year)',type:'number'},{key:'interestType',label:'Interest type',type:'select',options:['Flat','Reducing balance']}]
  },
  repayments:{
    title:'Repayments',description:'Track installment schedules, payment history, and early settlements.',action:'Record payment',
    columns:[{key:'loan',label:'LOAN / CUSTOMER'},{key:'due',label:'DUE DATE'},{key:'installment',label:'INSTALLMENT'},{key:'method',label:'METHOD'},{key:'status',label:'STATUS'}],
    rows:[{loan:'LN-2025-0718|Vikram Khanna',due:'18 Nov 2025',installment:'₹ 18,500',method:'UPI ·· 4821',status:'Paid'},{loan:'LN-2025-0654|Neha Kapoor',due:'18 Nov 2025',installment:'₹ 12,200',method:'Auto-debit',status:'Paid'},{loan:'LN-2025-0612|Aarav Shah',due:'18 Nov 2025',installment:'₹ 24,000',method:'Bank transfer',status:'Pending'},{loan:'LN-2025-0590|Meera Iyer',due:'17 Nov 2025',installment:'₹ 16,800',method:'Auto-debit',status:'Overdue'},{loan:'LN-2025-0544|Kabir Singh',due:'17 Nov 2025',installment:'₹ 9,400',method:'UPI ·· 7719',status:'Paid'}],
    fields:[{key:'loan',label:'Loan ID'},{key:'amount',label:'Payment amount',type:'number'},{key:'date',label:'Payment date',type:'date'},{key:'method',label:'Payment method',type:'select',options:['UPI','Bank transfer','Cash','Auto-debit']}]
  },
  collections:{
    title:'Collections',description:'Prioritize upcoming dues and follow recovery progress.',action:'Log collection',
    columns:[{key:'customer',label:'CUSTOMER / LOAN'},{key:'due',label:'DUE DATE'},{key:'installment',label:'INSTALLMENT'},{key:'amount',label:'TOTAL DUE'},{key:'status',label:'STATUS'}],
    rows:[], fields:[]
  },
  reports:{
    title:'Reports',description:'Explore portfolio performance and export operational reports.',action:'Generate report',
    columns:[{key:'report',label:'REPORT'},{key:'period',label:'PERIOD'},{key:'generated',label:'LAST GENERATED'},{key:'owner',label:'CREATED BY'},{key:'status',label:'STATUS'}],
    rows:[{report:'Loan summary',period:'October 2025',generated:'01 Nov 2025',owner:'Alex Morgan',status:'Ready'},{report:'Disbursement report',period:'October 2025',generated:'01 Nov 2025',owner:'Alex Morgan',status:'Ready'},{report:'Repayment report',period:'October 2025',generated:'01 Nov 2025',owner:'System',status:'Ready'},{report:'Overdue loan report',period:'As of today',generated:'Today, 08:00 AM',owner:'System',status:'Ready'},{report:'Customer report',period:'Q3 2025',generated:'01 Oct 2025',owner:'Alex Morgan',status:'Ready'}],
    fields:[{key:'report',label:'Report',type:'select',options:['Loan summary','Customer report','Disbursement report','Repayment report','Outstanding loan report','Overdue loan report']},{key:'from',label:'From date',type:'date'},{key:'to',label:'To date',type:'date'}]
  },
  users:{
    title:'Users & roles',description:'Manage team access, role permissions, and activity.',action:'Invite user',
    columns:[{key:'user',label:'TEAM MEMBER'},{key:'email',label:'EMAIL'},{key:'role',label:'ROLE'},{key:'lastActive',label:'LAST ACTIVE'},{key:'status',label:'STATUS'}],
    rows:[{user:'Alex Morgan|AM',email:'alex.morgan@northstar.in',role:'Administrator',lastActive:'Active now',status:'Active'},{user:'Rohan Desai|RD',email:'rohan.desai@northstar.in',role:'Collections manager',lastActive:'12 min ago',status:'Active'},{user:'Ananya Rao|AR',email:'ananya.rao@northstar.in',role:'Loan officer',lastActive:'1 hr ago',status:'Active'},{user:'Dev Malhotra|DM',email:'dev.malhotra@northstar.in',role:'Analyst',lastActive:'Yesterday',status:'Active'},{user:'Kavita Nair|KN',email:'kavita.nair@northstar.in',role:'Loan officer',lastActive:'08 Nov 2025',status:'Invited'}],
    fields:[{key:'name',label:'Full name'},{key:'email',label:'Work email',type:'email'},{key:'role',label:'Role',type:'select',options:['Administrator','Loan officer','Collections manager','Analyst']}]
  },
  settings:{
    title:'System settings',description:'Configure lending rules and preferences for your workspace.',action:'Save settings',
    columns:[{key:'setting',label:'CONFIGURATION'},{key:'description',label:'DESCRIPTION'},{key:'updated',label:'LAST UPDATED'},{key:'owner',label:'UPDATED BY'},{key:'status',label:'STATUS'}],
    rows:[{setting:'General settings',description:'Workspace name, timezone, and currency',updated:'12 Nov 2025',owner:'Alex Morgan',status:'Configured'},{setting:'Interest rates',description:'Rate bands by loan product',updated:'10 Nov 2025',owner:'Alex Morgan',status:'Configured'},{setting:'Loan configuration',description:'Tenure, limits, and eligibility rules',updated:'08 Nov 2025',owner:'Alex Morgan',status:'Configured'},{setting:'Payment settings',description:'Payment methods and retry schedule',updated:'04 Nov 2025',owner:'Alex Morgan',status:'Configured'},{setting:'Notifications',description:'Email and SMS templates',updated:'01 Nov 2025',owner:'Rohan Desai',status:'Configured'}],
    fields:[{key:'name',label:'Configuration name'},{key:'value',label:'Setting value'},{key:'notes',label:'Notes'}]
  }
};

@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin-page.component.html',
  styleUrl: './admin-page.component.css'
})
export class AdminPageComponent {
  private readonly route = inject(ActivatedRoute);
  readonly section = signal('customers');
  readonly query = signal('');
  readonly showDialog = signal(false);
  readonly detailRow = signal<PageRow | null>(null);
  readonly toast = signal('');
  readonly editingId = signal<number | null>(null);
  readonly loanCustomerOptions = signal<{ value: string; label: string }[]>([]);
  readonly loanProductOptions = signal(['Personal loan', 'Business loan', 'Vehicle loan', 'Home loan']);
  readonly previewMode = signal(true);
  readonly statusFilter = signal('All statuses');
  readonly sortKey = signal('');
  readonly sortAscending = signal(true);
  readonly pageIndex = signal(0);
  readonly definition = computed(() => definitions[this.section()] ?? definitions['customers']);
  readonly showLoanStatusField = computed(() => this.section() === 'loans' && this.editingId() !== null);
  readonly rows = signal<PageRow[]>([]);
  readonly filteredRows = computed(() => {
    const term = this.query().trim().toLowerCase();
    const status = this.statusFilter();
    const filtered = this.rows().filter(row => (!term || Object.values(row).some(value => value.toLowerCase().includes(term))) && (status === 'All statuses' || row['status'] === status));
    const key = this.sortKey();
    return key ? filtered.sort((left, right) => left[key].localeCompare(right[key], undefined, { numeric: true }) * (this.sortAscending() ? 1 : -1)) : filtered;
  });
  readonly statusOptions = computed(() => ['All statuses', ...new Set(this.rows().map(row => row['status']).filter((status): status is string => !!status))]);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filteredRows().length / 6)));
  readonly pageRows = computed(() => this.filteredRows().slice(this.pageIndex() * 6, this.pageIndex() * 6 + 6));
  readonly showingStart = computed(() => this.filteredRows().length ? this.pageIndex() * 6 + 1 : 0);
  readonly showingEnd = computed(() => Math.min((this.pageIndex() + 1) * 6, this.filteredRows().length));
  readonly pages = computed(() => Array.from({ length: this.pageCount() }, (_, index) => index));
  readonly attentionCount = computed(() => this.rows().filter(row => ['Under review','Overdue','Late fee due','Partially paid','Documents needed','Pending','Invited'].includes(row['status'])).length);
  form = new FormRecord<FormControl<string>>({});
  private readonly api = inject(LoanApiService);
  private readonly auth = inject(AuthService);

  constructor() {
    this.route.data.subscribe(data => {
      this.section.set(String(data['section'] ?? 'customers'));
      const hasBackend = ['customers', 'loans', 'collections'].includes(this.section());
      this.rows.set(hasBackend ? [] : [...this.definition().rows]);
      this.previewMode.set(!hasBackend);
      this.statusFilter.set('All statuses'); this.pageIndex.set(0); this.sortKey.set('');
      this.makeForm();
      this.loadRemoteRows();
      if (data['create']) this.openDialog();
    });
  }

  cell(row: PageRow, key: string): string { return row[key] ?? '—'; }
  canMutate(): boolean { return this.auth.hasRole('Administrator', 'LoanOfficer'); }
  setQuery(value: string): void { this.query.set(value); this.pageIndex.set(0); }
  setStatusFilter(value: string): void { this.statusFilter.set(value); this.pageIndex.set(0); }
  sortBy(key: string): void {
    if (this.sortKey() === key) this.sortAscending.update(value => !value);
    else { this.sortKey.set(key); this.sortAscending.set(true); }
  }
  changePage(offset: number): void { this.pageIndex.update(page => Math.max(0, Math.min(this.pageCount() - 1, page + offset))); }
  primary(value: string): string { return value.split('|')[0]; }
  secondary(value: string): string { return value.split('|')[1] ?? ''; }
  initials(value: string): string { return this.primary(value).split(' ').map(part => part[0] ?? '').slice(0, 2).join('').toUpperCase(); }
  stateClass(value: string): string {
    const normalized = value.toLowerCase();
    if (['active','paid','approved','disbursed','ready','configured','resolved'].includes(normalized)) return 'good';
    if (['overdue','rejected','escalated','inactive'].includes(normalized)) return 'bad';
    return 'pending';
  }
  disburse(row: PageRow): void {
    const id = Number(row['id']);
    if (!id) return;
    this.api.disburseLoan(id).subscribe({
      next: loan => { this.rows.update(rows => rows.map(item => Number(item['id']) === id ? this.loanRow(loan) : item)); this.notify('Loan disbursed and EMI schedule generated.'); },
      error: error => this.notify(this.errorMessage(error))
    });
  }
  reject(row: PageRow): void {
    const reason = window.prompt('Enter a rejection reason (5–500 characters):');
    if (reason === null) return;
    if (reason.trim().length < 5 || reason.trim().length > 500) { this.notify('Rejection reason must contain between 5 and 500 characters.'); return; }
    const id = Number(row['id']);
    if (!id) return;
    this.api.rejectLoan(id, reason.trim()).subscribe({
      next: loan => { this.rows.update(rows => rows.map(item => Number(item['id']) === id ? this.loanRow(loan) : item)); this.notify('Loan application rejected.'); },
      error: error => this.notify(this.errorMessage(error))
    });
  }
  makeForm(): void {
    const controls = new FormRecord<FormControl<string>>({});
    for (const field of this.definition().fields) {
      const validators = [Validators.required];
      if (field.key === 'interestRate') validators.push(Validators.min(0.01), Validators.max(100));
      if (field.key === 'requestedAmount') validators.push(Validators.min(1));
      if (field.key === 'tenureMonths') validators.push(Validators.min(1), Validators.max(360));
      controls.addControl(field.key, new FormControl('', { nonNullable: true, validators }));
    }
    this.form = controls;
  }
  openDialog(): void { this.editingId.set(null); this.makeForm(); this.showDialog.set(true); }
  openView(row: PageRow): void { this.detailRow.set(row); }
  closeView(): void { this.detailRow.set(null); }
  openEdit(row: PageRow): void {
    this.makeForm();
    this.editingId.set(row['id'] ? Number(row['id']) : null);
    if (this.section() === 'customers') this.form.addControl('rowVersion', new FormControl(row['rowVersion'] ?? '', { nonNullable: true, validators: [Validators.required] }));
    if (this.section() === 'loans') {
      if (!this.editingId()) { this.notify('Connect to the API to edit saved loan applications.'); return; }
      this.form.addControl('status', new FormControl('', { nonNullable: true, validators: [Validators.required] }));
      this.form.addControl('rowVersion', new FormControl(row['rowVersion'] ?? '', { nonNullable: true, validators: [Validators.required] }));
      const customerId = row['customerId'] ?? '';
      if (customerId && !this.loanCustomerOptions().some(option => option.value === customerId)) {
        this.loanCustomerOptions.update(options => [...options, { value: customerId, label: row['customerName'] ?? 'Current customer' }]);
      }
      this.form.patchValue({
        customerId,
        product: row['product'] ?? '',
        requestedAmount: row['requestedAmount'] ?? '',
        tenureMonths: row['tenureMonths'] ?? '',
        interestRate: row['interestRate'] ?? '',
        interestType: row['isFlatInterest'] === 'true' ? 'Flat' : 'Reducing balance',
        status: row['status'] ?? ''
      });
      this.form.controls['customerId'].disable();
      this.showDialog.set(true);
      return;
    }
    const values: Record<string, string> = {};
    const customer = this.primary(row['customer'] ?? row['application'] ?? row['user'] ?? '');
    for (const field of this.definition().fields) {
      if (field.key === 'fullName' || field.key === 'customer' || field.key === 'name') values[field.key] = customer;
      else if (field.key === 'phone') values[field.key] = row['phone'] ?? '';
      else if (field.key === 'email') values[field.key] = row['email'] ?? '';
      else if (field.key === 'city') values[field.key] = row['city'] ?? '';
      else if (field.key === 'status') values[field.key] = row['status'] ?? '';
      else if (field.key === 'product') values[field.key] = row['product'] ?? '';
      else if (field.key === 'amount') values[field.key] = (row['amount'] ?? '').replace(/[^0-9.]/g, '');
    }
    this.form.patchValue(values);
    this.showDialog.set(true);
  }
  closeDialog(): void { this.showDialog.set(false); this.editingId.set(null); }
  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const values = this.form.getRawValue() as Record<string, string>;
    if (this.section() === 'customers') {
      const request = { fullName: values['fullName'], phone: values['phone'], email: values['email'] || undefined, city: values['city'] || undefined, status: values['status'] || 'Active', ...(this.editingId() ? { rowVersion: values['rowVersion'] } : {}) };
      const request$ = this.editingId() ? this.api.updateCustomer(this.editingId()!, request) : this.api.createCustomer(request);
      request$.subscribe({
        next: customer => {
          this.rows.update(current => this.editingId() ? current.map(row => Number(row['id']) === customer.id ? this.customerRow(customer) : row) : [this.customerRow(customer), ...current]);
          this.finishSave();
        },
        error: error => this.notify(this.errorMessage(error))
      });
      return;
    }
    if (this.section() === 'loans') {
      const loanTerms = { product: values['product'], requestedAmount: Number(values['requestedAmount']), tenureMonths: Number(values['tenureMonths']), interestRate: Number(values['interestRate']), isFlatInterest: values['interestType'] === 'Flat' };
      const request$ = this.editingId()
        ? this.api.updateLoan(this.editingId()!, { ...loanTerms, status: values['status'] as LoanStatus, rowVersion: values['rowVersion'] })
        : this.api.createLoan({ ...loanTerms, customerId: Number(values['customerId']) });
      request$.subscribe({
        next: loan => {
          const row = this.loanRow(loan);
          this.rows.update(current => this.editingId() ? current.map(item => Number(item['id']) === loan.id ? row : item) : [row, ...current]);
          if (!this.editingId()) this.pageIndex.set(0);
          this.finishSave();
        },
        error: error => this.notify(this.errorMessage(error))
      });
      return;
    }
    this.addLocalRow(values);
    this.finishSave();
  }
  private addLocalRow(values: Record<string, string>): void {
    const columns = this.definition().columns;
    const row: PageRow = {};
    for (const column of columns) {
      const match = Object.entries(values).find(([key]) => key.toLowerCase().includes(column.key.toLowerCase()) || column.key.toLowerCase().includes(key.toLowerCase()));
      row[column.key] = match?.[1] || (column.key === 'status' ? 'Active' : column.key === 'customer' || column.key === 'application' || column.key === 'user' ? `${values['fullName'] ?? values['customer'] ?? values['name'] ?? 'New record'}|New record` : '—');
    }
    this.rows.update(current => [row, ...current]);
    this.pageIndex.set(0);
  }
  private finishSave(message?: string): void {
    this.showDialog.set(false);
    this.editingId.set(null);
    this.toast.set(message ?? 'Changes saved successfully.');
    setTimeout(() => this.toast.set(''), 3200);
  }
  private loadRemoteRows(): void {
    if (this.section() === 'customers') {
      this.api.customers().pipe(catchError(error => { this.notify(this.errorMessage(error)); return of(null); })).subscribe(customers => {
        if (customers) { this.rows.set(customers.map(customer => this.customerRow(customer))); this.previewMode.set(false); }
      });
    } else if (this.section() === 'loans') {
      this.api.products().pipe(catchError(error => { this.notify(this.errorMessage(error)); return of(null); })).subscribe(products => {
        if (products) this.loanProductOptions.set(products.filter(product => product.isActive).map(product => product.name));
      });
      this.api.customers().pipe(catchError(error => { this.notify(this.errorMessage(error)); return of([]); })).subscribe(customers => this.loanCustomerOptions.set(customers.filter(customer => customer.status === 'Active').map(customer => ({ value: String(customer.id), label: customer.fullName }))));
      this.api.loans().pipe(catchError(error => { this.notify(this.errorMessage(error)); return of(null); })).subscribe(loans => {
        if (loans) { this.rows.set(loans.map(loan => this.loanRow(loan))); this.previewMode.set(false); }
      });
    } else if (this.section() === 'collections') {
      this.api.overdueEmis().pipe(catchError(error => { this.rows.set([]); this.previewMode.set(false); this.notify(this.errorMessage(error)); return of(null); })).subscribe(items => {
        if (items) { this.rows.set(items.map(item => this.collectionRow(item))); this.previewMode.set(false); }
      });
    }
  }
  private customerRow(customer: CustomerDto): PageRow {
    return { id: String(customer.id), rowVersion: customer.rowVersion, customer: `${customer.fullName}|CUS-${String(customer.id).padStart(5, '0')}`, phone: customer.phone, email: customer.email ?? '—', city: customer.city ?? '—', loans: `${customer.loanCount} ${customer.loanCount === 1 ? 'loan' : 'loans'}`, joined: new Date(customer.createdAt).toLocaleDateString('en-IN'), status: customer.status };
  }
  private loanRow(loan: LoanDto): PageRow {
    return { id: String(loan.id), rowVersion: loan.rowVersion, customerId: String(loan.customerId), customerName: loan.customerName, application: `${loan.customerName}|${loan.applicationNumber}`, applicationNumber: loan.applicationNumber, product: loan.product, requestedAmount: String(loan.requestedAmount), tenureMonths: String(loan.tenureMonths), interestRate: String(loan.interestRate), isFlatInterest: String(loan.isFlatInterest), amount: `₹ ${loan.requestedAmount.toLocaleString('en-IN')}`, date: new Date(loan.appliedAt).toLocaleDateString('en-IN'), status: loan.status };
  }
  private collectionRow(item: OverdueEmiDto): PageRow {
    const status = item.emiOutstanding <= 0 ? 'Late fee due' : item.status === 'Partially paid' ? 'Partially paid' : 'Overdue';
    return { id: String(item.emiScheduleId), customer: `${item.customerName}|${item.applicationNumber}`, due: new Date(item.dueDate).toLocaleDateString('en-IN'), installment: `#${item.installmentNumber}`, amount: `₹ ${item.totalOutstanding.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`, status };
  }
  collectionTotal(): string {
    const total = this.rows().reduce((sum, row) => sum + Number((row['amount'] ?? '').replace(/[^\d.]/g, '')), 0);
    return `₹ ${total.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }
  remove(row: PageRow): void {
    const name = this.primary(row[this.definition().columns[0].key] ?? 'this record');
    const confirmation = this.section() === 'loans' ? `Archive loan application ${name}? It will be removed from active lists but retained in the database.` : `Remove ${name}? This action cannot be undone.`;
    if (!window.confirm(confirmation)) return;
    const removeLocal = () => this.rows.update(current => current.filter(item => item !== row));
    if (this.section() === 'loans' && row['id']) {
      this.api.deleteLoan(Number(row['id'])).subscribe({
        next: () => { removeLocal(); this.notify('Loan application archived.'); },
        error: error => this.notify(this.errorMessage(error))
      });
      return;
    }
    if (this.section() === 'customers' && row['id']) {
      this.api.deleteCustomer(Number(row['id'])).subscribe({
        next: () => { removeLocal(); this.notify('Customer removed.'); },
        error: error => this.notify(this.errorMessage(error))
      });
      return;
    }
    removeLocal();
    this.notify('Record removed from this preview session.');
  }
  private notify(message: string): void { this.toast.set(message); setTimeout(() => this.toast.set(''), 3200); }
  private errorMessage(error: unknown): string {
    const response = error as { error?: { message?: unknown; errors?: unknown } };
    const message = typeof response?.error?.message === 'string' ? response.error.message : '';
    const errors = Array.isArray(response?.error?.errors) ? response.error.errors.filter((item): item is string => typeof item === 'string') : [];
    return [message, ...errors].filter(Boolean).join(' ') || 'Could not complete the request. Check the API connection.';
  }
  exportCsv(): void {
    const columns = this.definition().columns;
    const lines = [columns.map(column => column.label).join(','), ...this.filteredRows().map(row => columns.map(column => `"${this.cell(row, column.key).replaceAll('"', '""')}"`).join(','))];
    const link = document.createElement('a');
    link.href = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' }));
    link.download = `${this.section()}-export.csv`;
    link.click();
    URL.revokeObjectURL(link.href);
  }
}
