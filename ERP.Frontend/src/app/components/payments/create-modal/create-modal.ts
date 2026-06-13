import { ChangeDetectorRef, Component, DestroyRef, Inject, inject, OnInit } from '@angular/core';
import { InvoiceDto, InvoiceService } from '../../../services/invoice.service';
import { debounceTime, distinctUntilChanged, forkJoin, map, of, Subject, switchMap, take } from 'rxjs';
import { ClientResponseDto } from '../../../services/clients/clients.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MatIcon, MatIconModule } from "@angular/material/icon";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule, Location } from '@angular/common';
import { CreatePaymentDto, InvoiceCacheDto, PaymentDto, PaymentService } from '../../../services/payment.service';
import { Router } from '@angular/router';

export enum PaymentMethod {
  ESPECE         = 'ESPECE',
  CHEQUE         = 'CHEQUE',
  VIREMENT       = 'VIREMENT',
  CARTE_BANCAIRE = 'CARTE_BANCAIRE',
  MOBILE_PAYMENT = 'MOBILE_PAYMENT',
  AUTRE          = 'AUTRE'
}

@Component({
  selector: 'app-create-payment-modal',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './create-modal.html',
  styleUrl: './create-modal.scss',
})
export class CreatePaymentModal implements OnInit{
  private dialogRef = inject(MatDialogRef<CreatePaymentModal>);
  private readonly destroyRef = inject(DestroyRef);
  private cdr = inject(ChangeDetectorRef);
  private translate = inject(TranslateService);

  form!: FormGroup;

  // CLIENT ID
  clientPage = 1;
  clientPageSize = 10;
  clientTotalCount = 0;
  clientsLoading = false;
  hasMoreClients = true;
  clientDropdownOpen = false;
  selectedClientLabel = '';
  clientSearchQuery = '';
  filteredClients: ClientResponseDto[] = [];
  clients: ClientResponseDto[] = [];
  private clientSearchSubject$ = new Subject<string>();

  // METHOD
  paymentMethods = Object.values(PaymentMethod);

  // INVOICES
  invoicePage = 1;
  invoicePageSize = 10;
  invoiceTotalCount = 0;
  invoicesLoading = false;
  hasMoreInvoices = true;
  invoiceDropdownOpen = false;
  selectedInvoiceLabel = '';
  invoiceSearchQuery = '';
  filteredInvoices: ClientResponseDto[] = [];
  invoices: (InvoiceDto & { remainingAmount: number })[] = [];
  private invoiceSearchSubject$ = new Subject<string>();

  successMessage: string | null = null;
  errorMessage:   string | null = null;

  isSubmitting = false;

  data = inject<{ mode: 'create' | 'edit' | 'refund'; payment?: PaymentDto }>(MAT_DIALOG_DATA);
  mode = this.data.mode;

  constructor(
    private invoiceService: InvoiceService,
    private paymentService: PaymentService    ,
    private fb: FormBuilder){}

  ngOnInit(): void {
    this.initForm();

    if (this.mode !== 'create' && this.data.payment) {
      this.patchForm(this.data.payment);
    }

    this.clientSearchSubject$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(query => {
      this.filterClients(query);        // filter local list
      this.clientSearchQuery = query;
      this.loadClients(1, false);       // also fetch from server with new query
    });
    this.loadClients(1);
    this.cdr.markForCheck();
  }

  initForm() {
    this.form = this.fb.group({
      clientId:          ['', Validators.required],
      method:            ['', Validators.required],
      paymentDate:        [this.toLocalDateString(null), Validators.required],
      externalReference: [null],
      notes:             [null],
      totalAmount:       [0, [Validators.required, Validators.min(0)]],
      allocations:             this.fb.array([this.createLine()]) // ← FormArray
    });
  }

  patchForm(payment: PaymentDto): void {
    this.form.patchValue({
      clientId:          payment.clientId,
      method:            payment.method,
      paymentDate:       this.toLocalDateString(payment.paymentDate),
      externalReference: payment.externalReference ?? null,
      notes:             payment.notes ?? null,
      totalAmount:       payment.totalAmount,
    });

    // Fetch the client directly — don't depend on the filtered unpaid-clients list
    this.invoiceService.getClientById(payment.clientId).pipe(take(1)).subscribe({
      next: (client) => {
        if (client) {
          this.selectedClientLabel = `${client.name} - ${client.email}`;
          this.cdr.markForCheck();
        }
      },
      error: () => {
        // Fallback — at least show something readable
        this.selectedClientLabel = payment.clientId;
      }
    });

    this.loadInvoices(payment.clientId);

    this.allocations.clear();
    payment.allocations.forEach(a => {
      this.allocations.push(
        this.fb.group({
          invoiceId:       [a.invoiceId,       Validators.required],
          allocatedAmount: [a.amountAllocated, [Validators.required, Validators.min(0.01)]]
        })
      );
    });

    if (this.mode === 'edit') {
      this.allocations.controls.forEach(ctrl => ctrl.disable());
      this.form.get('clientId')?.disable();
      this.form.get('totalAmount')?.disable();
    }

    this.cdr.markForCheck();
  }

  loadInvoices(clientId: string): void {
    this.invoicesLoading = true;
    this.invoices = [];

    forkJoin({
      invoices: this.invoiceService.getByClientId(clientId, 1, 100),
      cache:    this.paymentService.getInvoicesCacheByClient(clientId, 1, 100)
    }).pipe(take(1)).subscribe({
      next: ({ invoices, cache }) => {
        const cacheMap = new Map(cache.items.map(c => [c.id, c]));

        this.invoices = invoices.items
          .filter(inv => inv.status === 'UNPAID')
          .map(inv => ({
            ...inv,
            remainingAmount: cacheMap.get(inv.id)?.remainingAmount ?? inv.totalTTC
          }));

        this.invoicesLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.invoicesLoading = false;
        const msg = err?.error?.message ?? this.translate.instant('invoices.responses.errors.load_failed');
        this.flash('error', msg);
        this.cdr.markForCheck();
      }
    });
  }


  loadMoreInvoices(clientId: string): void {
    if (!this.hasMoreInvoices || this.invoicesLoading) return;
    this.invoicePage++;

    forkJoin({
      invoices: this.invoiceService.getByClientId(clientId, this.invoicePage, this.invoicePageSize),
      cache:    this.paymentService.getInvoicesCacheByClient(clientId, this.invoicePage, this.invoicePageSize)
    }).pipe(take(1)).subscribe({
      next: ({ invoices, cache }) => {
        const cacheMap = new Map(cache.items.map(c => [c.id, c]));

        const newItems = invoices.items
          .filter(inv => inv.status === 'UNPAID')
          .map(inv => ({
            ...inv,
            remainingAmount: cacheMap.get(inv.id)?.remainingAmount ?? inv.totalTTC
          }));

        this.invoices = [...this.invoices, ...newItems];
        this.hasMoreInvoices = this.invoices.length < this.invoiceTotalCount;
        this.cdr.markForCheck();
      }
    });
  }

  loadClients(page: number, append = false): void {
    if (this.clientsLoading) return;
    this.clientsLoading = true;

    // Step 1: fetch unpaid invoices to get the client IDs
    this.paymentService.getInvoicesCacheByStatus('UNPAID', 1, 1000)
      .pipe(
        switchMap(unpaidResult => {
          // Extract unique client IDs from unpaid invoices
          const unpaidClientIds = new Set(
            unpaidResult.items.map(inv => inv.clientId)
          );
          if (unpaidClientIds.size === 0) {
            // No unpaid invoices — no clients to show
            return of({ items: [], totalCount: 0, unpaidClientIds });
          }

          // Step 2: fetch clients page, carry unpaidClientIds along
          return this.invoiceService.getClientsPaged(
            page,
            this.clientPageSize,
            this.clientSearchQuery
          ).pipe(
            map(clientResult => ({ ...clientResult, unpaidClientIds }))
          );
        }),
        take(1)
      )
      .subscribe({
        next: ({ items, totalCount, unpaidClientIds }) => {
          const filtered = items.filter(c => unpaidClientIds.has(c.id));
          this.clients = append ? [...this.clients, ...filtered] : filtered;
          this.clientTotalCount = filtered.length;
          this.clientPage = page;
          this.hasMoreClients = this.clients.length < totalCount;
          this.clientsLoading = false;

          if (!append && this.mode === 'create' &&
              this.clients.length > 0 && !this.form?.get('clientId')?.value) {
            setTimeout(() => this.selectClient(this.clients[0])); // ← defers past CD
          }

          this.cdr.markForCheck();
        },
        error: (err) => {
          this.clientsLoading = false;
          const msg = err?.error?.message ?? this.translate.instant('clients.responses.errors.load_failed');
          this.flash('error', msg);
          this.cdr.markForCheck();
        }
      });
  }


  selectClient(client: ClientResponseDto): void {
    this.form.patchValue({ clientId: client.id });
    this.selectedClientLabel = `${client.name} - ${client.email}`;
    this.clientDropdownOpen  = false;
    this.clientSearchQuery   = client.name;
    this.filteredClients     = [];

    this.allocations.clear();
    this.allocations.push(this.createLine());
    this.loadInvoices(client.id);   // ← fetch here
    this.cdr.markForCheck();
  }

  filterClients(query: string): void {
    if (!query || query.length < 2) { this.filteredClients = []; return; }
    const q = query.toLowerCase();
    this.filteredClients = this.clients
      .filter(c => c.name?.toLowerCase().includes(q) || c.email?.toLowerCase().includes(q))
      .slice(0, 8);
  }

  onClientSearch(query: string): void {
    this.clientSearchSubject$.next(query);
  }

  loadMoreClients(): void {
    if (!this.hasMoreClients || this.clientsLoading) return;
    this.loadClients(this.clientPage + 1, true);
  }

  toggleClientDropdown(): void {
    this.clientDropdownOpen = !this.clientDropdownOpen;
    if (this.clientDropdownOpen) {
      this.clientSearchQuery = '';
      this.loadClients(1, false);
    }
  }

  get selectedInvoiceIds(): Set<string> {
    return new Set(
      this.allocations.controls
        .map(line => line.get('invoiceId')?.value)
        .filter(id => !!id)
    );
  }

  getMaxForLine(lineIndex: number): number {
    const invoiceId = this.getAllocationLine(lineIndex).get('invoiceId')?.value;
    if (!invoiceId) return Infinity;
    return this.invoices.find(inv => inv.id === invoiceId)?.remainingAmount ?? Infinity;
  }

  onInvoiceSelected(lineIndex: number): void {
    const max = this.getMaxForLine(lineIndex);
    const amountControl = this.getAllocationLine(lineIndex).get('allocatedAmount');
    amountControl?.setValidators([
      Validators.required,
      Validators.min(0.01),
      Validators.max(max)
    ]);
    amountControl?.updateValueAndValidity();
  }

  availableInvoicesForLine(lineIndex: number): (InvoiceDto & { remainingAmount: number })[] {
    const currentLineInvoiceId = this.getAllocationLine(lineIndex).get('invoiceId')?.value;
    return this.invoices.filter(inv =>
      !this.selectedInvoiceIds.has(inv.id) || inv.id === currentLineInvoiceId
    );
  }

  get allocations(): FormArray {
    return this.form.get('allocations') as FormArray;
  }

  getAllocationLine(index: number): FormGroup {
    return this.allocations.at(index) as FormGroup;
  }

  createLine(): FormGroup {
    return this.fb.group({
      invoiceId:       ['', Validators.required],
      allocatedAmount: [0, [Validators.required, Validators.min(0)]]
    });
  }

  addLine(): void {
    this.allocations.push(this.createLine());
  }

  removeLine(index: number): void {
    if (this.allocations.length > 1) {
      this.allocations.removeAt(index);
      this.onAllocatedAmountChange(); // ← recalculate total after removal
    }
  }

  completeLine(index: number): void {
    if (index < 0 || index >= this.allocations.length) return;

    const line = this.getAllocationLine(index);
    const invoiceId = line.get('invoiceId')?.value;
    if (!invoiceId) return;

    const invoice = this.invoices.find(inv => inv.id === invoiceId);
    if (!invoice) return;

    line.patchValue({ allocatedAmount: invoice.remainingAmount });
    this.onAllocatedAmountChange();
    this.cdr.markForCheck();
  }

  onAllocatedAmountChange(): void {
    const total = this.allocations.controls.reduce((sum, line) => {
      const val = parseFloat(line.get('allocatedAmount')?.value ?? 0);
      return sum + (isNaN(val) ? 0 : val);
    }, 0);

    // Round to 2 decimals to kill floating-point drift
    this.form.get('totalAmount')?.setValue(
      Math.round(total * 100) / 100
    );
  }


  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();

    if(this.mode === 'create'){
      const dto: CreatePaymentDto = {
        clientId:          raw.clientId,
        totalAmount:       raw.totalAmount,
        method:            raw.method,
        paymentDate:       raw.paymentDate,
        externalReference: raw.externalReference ?? undefined,
        notes:             raw.notes ?? undefined,
        allocations:       raw.allocations.map((line: any) => ({
          invoiceId:       line.invoiceId,
          amountAllocated: line.allocatedAmount
        }))
      };

      this.isSubmitting = true;

      this.paymentService.createPayment(dto)
        .pipe(take(1))
        .subscribe({
          next: (payment) => {
            this.isSubmitting = false;
            this.flash('success', this.translate.instant('payments.responses.success.created'));
            setTimeout(() =>
              this.dialogRef.close(payment), 1500
            );
          },
          error: (err) => {
            this.isSubmitting = false;
            const msg = err?.error?.message ?? this.translate.instant('payments.errors.create_failed');
            this.flash('error', msg);
          }
        });
    }

    if(this.mode==='edit'){
      this.paymentService.correctPaymentDetails(this.data.payment!.id, {
        paymentDate: raw.paymentDate,
        method: raw.method,
        externalReference: raw.externalReference,
        notes: raw.notes
      }).subscribe(res => this.dialogRef.close(res));
    }
  }

  cancel(): void {
    this.dialogRef.close(); // ← close with no result
  }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => {
        document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 0);
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errorMessage = msg;
      setTimeout(() => {
        document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 0);
      setTimeout(() => { this.errorMessage = null; }, 4000);
    }
  }

  private toLocalDateString(dateStr: string | null | undefined): string {
    if (!dateStr) return new Date().toISOString().split('T')[0];
    // Append local midnight to prevent UTC parsing
    const raw = dateStr.includes('T') ? dateStr : `${dateStr}T00:00:00`;
    const d   = new Date(raw);
    const yyyy = d.getFullYear();
    const mm   = String(d.getMonth() + 1).padStart(2, '0');
    const dd   = String(d.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }
}