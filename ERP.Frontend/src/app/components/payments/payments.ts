import { ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, FormArray, FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PaginationComponent } from '../pagination/pagination';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { InvoiceCacheDto, PaymentDto, PaymentService, PaymentStatsDto, PaymentStatus } from '../../services/payment.service';
import { InvoiceDto, InvoiceService } from '../../services/invoice.service';
import { HttpError } from '../../interfaces/HttpError';
import { forkJoin } from 'rxjs';
import { ClientResponseDto, ClientsService } from '../../services/clients/clients.service';
import { AuthService, PRIVILEGES } from '../../services/auth/auth.service';
import { CreatePaymentModal } from './create-modal/create-modal';

type InvoiceWithPayment = InvoiceDto & {
  paidAmount: number;
  remainingAmount: number;
};

export type PaymentFormMode = 'create' | 'edit';

export interface PaymentFormData {
  payment?: PaymentDto;
  mode: PaymentFormMode;
}

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatDialogModule,
    PaginationComponent,
    TranslatePipe,
    RouterLink
  ],
  templateUrl: './payments.html',
  styleUrl: './payments.scss'
})
export class PaymentComponent implements OnInit {
  private cdr= inject(ChangeDetectorRef);

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null;

  payments: PaymentDto[]= [];

  invoicesCached: InvoiceWithPayment[] = [];
  clients: ClientResponseDto[]= []

  readonly PRIVILEGES = PRIVILEGES;

  // ── Filters / sort ─────────────────────────────────────────────────────────
  searchQuery = '';
  sortColumn = '';
  statusFilter: PaymentStatus= 'DONE';
  sortDirection: 'asc' | 'desc' = 'asc';

  get sortedData(): PaymentDto[] {
    let data = [...this.payments];
    if (this.searchQuery) {
      const q = this.searchQuery.toLowerCase();
      data = data.filter(p =>
        p.number.toLowerCase().includes(q)
      );
    }
    if (this.sortColumn) {
      data.sort((a, b) => {
        const av = (a as any)[this.sortColumn];
        const bv = (b as any)[this.sortColumn];
        const cmp = av < bv ? -1 : av > bv ? 1 : 0;
        return this.sortDirection === 'asc' ? cmp : -cmp;
      });
    }
    return data;
  }

  // state
  isLoading= false;
  totalCount = 0;
  currentPage = 1;
  currentSize = 10;
  readonly pageSizeOptions = [5, 10, 25, 50];
  get totalPages(): number { return Math.ceil(this.totalCount / this.currentSize) || 1; }
  stats: PaymentStatsDto= {done:0, cancelled:0};


  constructor(
    private dialog: MatDialog,
    public authService: AuthService,
    private clientService: ClientsService,
    private invoiceService: InvoiceService,
    private paymentService: PaymentService,
    private translate: TranslateService){}




  ngOnInit(){
    this.reload();
  }

  reload(): void{

    forkJoin({
      invoices: this.invoiceService.getAll(1, 100),
      invoiceCache: this.paymentService.getInvoicesCacheByStatus('UNPAID',1, 100),
      clients: this.clientService.getAll(1, 100),
      payments: this.paymentService.getPaymentsPaged(this.currentPage, this.currentSize, this.statusFilter),
      stats: this.paymentService.getPaymentStats()
    }).subscribe({
      next: ({payments, stats, clients, invoiceCache, invoices})=>{
        this.payments=payments.items;
        this.clients= clients.items;

        const cacheMap = new Map(
          invoiceCache.items.map(c => [c.id, c])
        );
        this.invoicesCached = invoices.items.map(inv => {
          const cache = cacheMap.get(inv.id);

          return {
            ...inv,
            paidAmount: cache?.paidAmount ?? 0,
            remainingAmount: cache?.remainingAmount ?? inv.totalTTC
          };
        });

        this.stats= stats;

        this.cdr.markForCheck();
      },
      error:()=>{
        this.flash("error", this.translate.instant('INVOICES.ERRORS.LOAD_FAILED'));
        this.cdr.markForCheck();
      }

    });
  }

  cancel(payment: PaymentDto){
    this.paymentService.cancelPayment(payment.id).subscribe({
      next:()=>{
        this.flash('success', this.translate.instant('PAYMENTS.SUCCESS.CANCELLED'));
        this.reload();
      },
      error:()=>{
        this.flash('error', this.translate.instant('PAYMENTS.ERRORS.CANCELLED'));
      }
    })
  }


  sortBy(col: string): void {
    this.sortDirection = this.sortColumn === col && this.sortDirection === 'asc' ? 'desc' : 'asc';
    this.sortColumn = col;
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.currentSize = size;
    this.currentPage = 1;
    this.reload();
  }

  getUnpaidInvoices(): InvoiceWithPayment[]{
    return this.invoicesCached.filter(inv=> inv.status === 'UNPAID');
  }

  getClientById(id: string): ClientResponseDto | null {
    return this.clients.find(clt => clt.id === id) ?? null;
  }

  getByInvoiceId(id: string): InvoiceWithPayment | null{
    return this.invoicesCached.find(inv=> inv.id === id) ?? null;
  }

  openCreate(){
    this.cdr.markForCheck();
    const ref = this.dialog.open(CreatePaymentModal, {
      width: '640px',
      maxWidth: '72vw',
      disableClose: true,   // ← requires explicit cancel/submit
      data: { mode: 'create' }
    });
    this.cdr.markForCheck();

    ref.afterClosed().subscribe((payment)=>{
      if(payment) this.reload();
    }
    );
  }

  correctDetails(payment: PaymentDto){
    this.cdr.markForCheck();
    const ref = this.dialog.open(CreatePaymentModal, {
      width: '640px',
      maxWidth: '72vw',
      disableClose: true,   // ← requires explicit cancel/submit
      data: { mode: 'edit', payment }
    });
    this.cdr.markForCheck();

    ref.afterClosed().subscribe((payment)=>{
      if(payment) this.reload();
    });
  }

  setStatusFilter(status: PaymentStatus){
    this.statusFilter= status;
  }

  trackById(_: number, item: { id: string }) { return item.id; }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; }, 4000);
    }
  }

  getAddButtonTooltip(): string {
    return this.invoicesCached.length === 0 ? this.translate.instant('PAYMENTS.ERRORS.UNPAID_INVOICES_NOT_FOUND') : '';
  }
  dismissError(): void { this.errors = []; }

}