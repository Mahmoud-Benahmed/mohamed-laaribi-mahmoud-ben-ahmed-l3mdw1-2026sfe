import { ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PaginationComponent } from '../../pagination/pagination';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  InvoiceCacheDto,
  PaymentService,
  RefundRequestDto,
  RefundStatsDto,
} from './../../../services/payment.service';
import { ClientResponseDto, ClientsService } from './../../../services/clients/clients.service';
import { AuthService, PRIVILEGES } from './../../../services/auth/auth.service';
import { forkJoin, take } from 'rxjs';
import { CompleteRefundModal } from './complete-modal/complete-modal';
import { InvoiceDto, InvoiceService } from '../../../services/invoice.service';

@Component({
  selector: 'app-refunds',
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
    RouterLink,
  ],
  templateUrl: './refund.html',
  styleUrl: './refund.scss',
})
export class RefundsComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null;

  private invoiceCache = new Map<string, InvoiceDto>();
  invoiceMap = signal<Map<string, InvoiceDto>>(new Map());

  // ── Data ──────────────────────────────────────────────────────────────────
  refunds: RefundRequestDto[] = [];
  clients: ClientResponseDto[] = [];

  readonly PRIVILEGES = PRIVILEGES;

  completeForm!: FormGroup;

  // ── Filters / sort ────────────────────────────────────────────────────────
  searchQuery = '';
  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';
  statusFilter: 'ALL' | 'PENDING' | 'COMPLETED' = 'ALL';

  get sortedData(): RefundRequestDto[] {
    let data = [...this.refunds];

    if (this.statusFilter !== 'ALL') {
      data = data.filter(r => r.status === this.statusFilter);
    }

    if (this.searchQuery) {
      const q = this.searchQuery.toLowerCase();
      data = data.filter(r =>
        r.id.toLowerCase().includes(q) ||
        this.getClientById(r.clientId)?.name?.toLowerCase().includes(q)
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

  // ── Pagination ────────────────────────────────────────────────────────────
  isLoading = false;
  totalCount = 0;
  currentPage = 1;
  currentSize = 10;
  readonly pageSizeOptions = [5, 10, 25, 50];
  get totalPages(): number { return Math.ceil(this.totalCount / this.currentSize) || 1; }

  stats: RefundStatsDto = { totalCount: 0, completedCount: 0, pendingCount: 0 };

  // ── Complete modal state ───────────────────────────────────────────────────
  completingRefund: RefundRequestDto | null = null;
  externalReference = '';
  isSubmitting = false;

  constructor(
    private dialog: MatDialog,
    public authService: AuthService,
    private clientService: ClientsService,
    private paymentService: PaymentService,
    private translate: TranslateService,
    private fb: FormBuilder,
    private invoiceService: InvoiceService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    forkJoin({
      clients: this.clientService.getAll(1, 200),
      stats:   this.paymentService.getRefundStats(),
    }).subscribe({
      next: ({ clients, stats }) => {
        this.clients = clients.items;
        this.stats   = stats;
        this.loadRefunds();
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', this.translate.instant('REFUNDS.ERRORS.LOAD_FAILED'));
        this.cdr.markForCheck();
      },
    });
  }

  private loadRefunds(): void {
    // Load refunds per client and flatten — adapt if you add a paged endpoint later
    const clientIds = this.clients.map(c => c.id);
    if (!clientIds.length) { this.refunds = []; return; }

    const calls = clientIds.map(id => this.paymentService.getRefundsByClientId(id));
    forkJoin(calls).subscribe({
      next: (results) => {
        this.refunds    = results.flat();
        this.totalCount = this.refunds.length;
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', this.translate.instant('REFUNDS.ERRORS.LOAD_FAILED'));
        this.cdr.markForCheck();
      },
    });
  }

  // ── Complete flow ─────────────────────────────────────────────────────────


  openComplete(completingRefund: RefundRequestDto): void {
    const ref = this.dialog.open(CompleteRefundModal, {
      width: '480px',
      disableClose: true,   // ← requires explicit cancel/submit
      data: { refund: completingRefund }
    });
    this.cdr.markForCheck();

    ref.afterClosed().subscribe((result) => {
        if(result) this.reload();
    });
  }

  // ── Filters / sort ────────────────────────────────────────────────────────

  setStatusFilter(status: 'ALL' | 'PENDING' | 'COMPLETED'): void {
    this.statusFilter = status;
  }

  sortBy(col: string): void {
    this.sortDirection = this.sortColumn === col && this.sortDirection === 'asc' ? 'desc' : 'asc';
    this.sortColumn    = col;
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  onPageChange(page: number): void {
    this.currentPage = page;
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.currentSize = size;
    this.currentPage = 1;
    this.reload();
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  getClientById(id: string): ClientResponseDto | null {
    return this.clients.find(c => c.id === id) ?? null;
  }

  getTotalRefundAmount(refund: RefundRequestDto): number {
    return refund.lines.reduce((sum, l) => sum + l.amount, 0);
  }

  getInvoiceById(id: string): InvoiceDto | null {
    if (this.invoiceCache.has(id)) {
      return this.invoiceCache.get(id)!;
    }

    this.invoiceService.getById(id)
      .pipe(take(1))
      .subscribe({
        next: (invoice) => {
          this.invoiceCache.set(id, invoice);
          this.invoiceMap.set(new Map(this.invoiceCache));
        },
        error: () => {
          this.invoiceCache.set(id, null!); // prevent retrying on every render
        }
      });

    return null; // placeholder while loading
  }

  statusClass(status: string): Record<string, boolean> {
    return {
      'badge--amber': status === 'PENDING',
      'badge--green': status === 'COMPLETED',
    };
  }

  trackById(_: number, item: { id: string }) { return item.id; }

  // ── Alerts ────────────────────────────────────────────────────────────────

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; }, 4000);
    }
  }

  dismissError(): void { this.errors = []; }
}