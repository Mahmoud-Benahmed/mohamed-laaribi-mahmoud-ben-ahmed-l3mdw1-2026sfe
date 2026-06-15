import { Component, OnInit, OnDestroy, signal, computed, inject, DestroyRef, ViewChild, ElementRef, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, map, Observable, of } from 'rxjs';
import { RouterLink } from '@angular/router';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

import { AuthService, PRIVILEGES } from '../../services/auth/auth.service';
import { InvoiceService, InvoiceDto, CreateInvoiceDto, InvoiceStatsDto } from '../../services/invoice.service';
import { ArticleService, UnitEnum } from '../../services/articles/articles.service';
import { StockItem, StockService } from '../../services/stock.service';
import { PaginationComponent } from '../pagination/pagination';
import { ModalComponent } from '../modal/modal';
import { HttpError } from '../../interfaces/HttpError';
import { ClientResponseDto } from '../../services/clients/clients.service';

type ViewMode = 'list' | 'list-deleted' | 'stats';

Chart.register(...registerables);

@Component({
  selector: 'app-invoices',
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
  templateUrl: './invoices.html',
  styleUrl: './invoices.scss',
})
export class InvoicesComponent implements OnInit, OnDestroy {
  @ViewChild('monthlyChart') monthlyChartRef!: ElementRef<HTMLCanvasElement>;

  private chart: Chart | null = null;
  private themeObserver: MutationObserver | null = null;
  private readonly destroyRef = inject(DestroyRef);
  private readonly translate = inject(TranslateService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly PRIVILEGES = PRIVILEGES;
  readonly units = UnitEnum;

  // ── View mode ──────────────────────────────────────────────────────────────
  viewMode = signal<ViewMode>('list');
  isList      = computed(() => this.viewMode() === 'list');
  isDeletedList = computed(() => this.viewMode() === 'list-deleted');
  isStats     = computed(() => this.viewMode() === 'stats');

  // ── Invoice list state ─────────────────────────────────────────────────────
  invoices: InvoiceDto[] = [];
  deletedInvoices: InvoiceDto[] = [];
  totalCount = 0;
  currentPage = 1;
  currentSize = 10;
  readonly pageSizeOptions = [5, 10, 25, 50];
  get totalPages(): number { return Math.ceil(this.totalCount / this.currentSize) || 1; }

  // ── Stats ──────────────────────────────────────────────────────────────────
  stats = { total: 0, draft: 0, unpaid: 0, paid: 0, cancelled: 0, deleted: 0 };
  invoiceStats: InvoiceStatsDto | null = null;
  statsLoading = false;

  clients: ClientResponseDto[] = [];

  // ── Filters / sort ─────────────────────────────────────────────────────────
  searchQuery = '';
  statusFilter = 'ALL';
  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  get sortedData(): InvoiceDto[] {
    let data = this.isDeletedList() ? [...this.deletedInvoices] : [...this.invoices];
    if (this.searchQuery) {
      const q = this.searchQuery.toLowerCase();
      data = data.filter(i =>
        i.invoiceNumber.toLowerCase().includes(q) ||
        i.clientFullName.toLowerCase().includes(q)
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

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null;

  // ── Client / Article data ─────────────────────────────────────────────────
  articles: StockItem[] = [];
  private masterArticles: StockItem[] = [];

  constructor(
    public authService: AuthService,
    private invoiceService: InvoiceService,
    private dialog: MatDialog,
    private stock: StockService
  ) {}

  ngOnInit(): void {
      // load articles + invoices + stats in parallel
      forkJoin({
          articles: this.loadArticlesWithStock(),
          clients: this.stock.getClientsPaged(1, 100).pipe(catchError(() => of({ items: [] }))),
          invoices: this.invoiceService.getAll(this.currentPage, this.currentSize),
          stats: this.invoiceService.getStats()
      }).subscribe({
          next: ({ invoices, clients, stats }) => {
              this.invoices   = invoices.items.filter(inv=> !inv.isDeleted);
              this.clients = clients.items;
              this.totalCount = this.invoices.length ?? 0;
              this.stats = {
                total: stats.totalInvoices,
                cancelled: stats.cancelledCount,
                deleted: stats.deletedCount,
                draft: stats.draftCount,
                paid: stats.paidCount,
                unpaid: stats.unpaidCount
              }
              this.cdr.markForCheck();

          },
          error: () => {
              this.invoices = [];
              this.cdr.markForCheck();
              this.flash('error', this.translate.instant('invoices.responses.errors.load_failed'));
          }
      });
  }

  ngAfterViewInit(): void {
    this.observeThemeChanges();
  }

  ngOnDestroy(): void {
    this.themeObserver?.disconnect();
    this.chart?.destroy();
  }

  // ── Load data ─────────────────────────────────────────────────────────────
  reload(): void {
    forkJoin({
      articles: this.loadArticlesWithStock(),
    }).subscribe({
      next: () => {
        this.cdr.markForCheck();
        this.loadCurrentView();
      },
      error: () => {
        this.cdr.markForCheck();
        this.loadCurrentView();
      }
    });
  }

  loadDeletedInvoices(): void {
    this.invoiceService.getAll(this.currentPage, this.currentSize, true).subscribe({
      next: (res) => {
        this.deletedInvoices = res.items.filter(i => i.isDeleted); // ← isDeleted not !isDeleted
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', this.translate.instant('invoices.responses.errors.load_failed'));
      }
    });
  }

  private loadCurrentView(): void {
    if (this.isDeletedList()) this.loadDeletedInvoices();
    else if (this.isStats()) this.loadStats();
    else this.load();
  }

  load(): void {
    const req$ = this.statusFilter === 'ALL'
      ? this.invoiceService.getAll(this.currentPage, this.currentSize)
      : this.invoiceService.getByStatus(this.statusFilter, this.currentPage, this.currentSize);

    req$.subscribe({
      next: res => {
        this.invoices   = Array.isArray(res.items) ? res.items : [];
        this.totalCount = res.totalCount ?? 0;
        // ← remove deletedInvoices and stats.deleted assignment here

        if (this.statusFilter === 'ALL') this.loadStats(); // ← stats.deleted comes from here

        this.cdr.markForCheck();
      },
      error: () => {
        this.invoices   = [];
        this.totalCount = 0;
        this.cdr.markForCheck();
        this.flash('error', this.translate.instant('invoices.responses.errors.load_failed'));
      },
    });
  }

  loadStats(): void {
    this.statsLoading = true;
    this.invoiceStats = null;
    this.invoiceService.getStats().subscribe({
      next: stats => {
        this.stats = {
          total: stats.totalInvoices,
          cancelled: stats.cancelledCount,
          deleted: stats.deletedCount,
          draft: stats.draftCount,
          paid: stats.paidCount,
          unpaid: stats.unpaidCount
        }
        this.invoiceStats = stats;
        this.statsLoading = false;
        setTimeout(() => this.renderStatusPieChart(), 100);
      },
      error: () => {
        this.flash('error', this.translate.instant('invoices.responses.errors.load_stats_failed'));
        this.statsLoading = false;
      },
    });
  }

  private loadArticlesWithStock(): Observable<StockItem[]> {
    return forkJoin({
      articles: this.invoiceService.getArticlesPaged(1, 20).pipe(catchError(() => of({ items: [] }))),
      stock: this.stock.getStockArticles().pipe(catchError(() => of({ inStock: [], outStock: [] })))
    }).pipe(
      map(({ articles, stock }) => {
        const stockMap = new Map<string, number>();
        stock.inStock.forEach((s: any) => stockMap.set(s.articleId || s.id, s.quantity));

        this.masterArticles = (articles.items || [])
          .filter((a: any) => stockMap.has(a.id) && stockMap.get(a.id)! > 0)
          .map((a: any) => ({ ...a, quantity: stockMap.get(a.id)! }));

        this.articles = [...this.masterArticles];
        return this.articles;
      }),
      catchError(() => {
        this.articles = [];
        return of([]);
      })
    );
  }

  // ── Navigation ────────────────────────────────────────────────────────────
  openStats(): void {
    if (this.isStats()) return;
    this.viewMode.set('stats');
    this.loadStats();
  }

  cancel(): void {
    this.viewMode.set('list');
    this.reload();
  }

  // ── Filters / sort ────────────────────────────────────────────────────────
  setStatusFilter(status: string): void {
    this.statusFilter = status;
    this.currentPage = 1;
    this.load();
  }

  sortBy(col: string): void {
    this.sortDirection = this.sortColumn === col && this.sortDirection === 'asc' ? 'desc' : 'asc';
    this.sortColumn = col;
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.isDeletedList() ? this.loadDeletedInvoices() : this.load();
  }

  onPageSizeChange(size: number): void {
    this.currentSize = size;
    this.currentPage = 1;
    this.isDeletedList() ? this.loadDeletedInvoices() : this.load();
  }

  // ── CRUD actions ──────────────────────────────────────────────────────────
  delete(invoice: InvoiceDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'delete', iconColor: 'warn',
        title: this.translate.instant('invoices.dialog.delete_invoice_title'),
        message: this.translate.instant('invoices.dialog.delete_invoice_message', { number: invoice.invoiceNumber }),
        confirmText: this.translate.instant('invoices.dialog.delete_confirm'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });
    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;
      this.invoiceService.delete(invoice.id).subscribe({
        next: () => {
          this.flash('success', this.translate.instant('invoices.responses.success.deleted'));
          this.reload();
        },
        error: () => this.flash('error', this.translate.instant('invoices.responses.errors.delete_failed')),
      });
    });
  }

  restore(invoice: InvoiceDto): void {
    this.invoiceService.restore(invoice.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('invoices.responses.success.restored'));
        this.isDeletedList() ? this.loadDeletedInvoices() : this.reload();
      },
      error: () => this.flash('error', this.translate.instant('invoices.responses.errors.restore_failed')),
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
  isDateOverdue(date: string | Date): boolean {
    if (!date) return false;
    const today = new Date();
    const d = new Date(date);
    today.setHours(0, 0, 0, 0);
    d.setHours(0, 0, 0, 0);
    return d < today;
  }

  statusClass(status: string): Record<string, boolean> {
    return {
      'badge--amber': status === 'DRAFT',
      'badge--red':   status === 'UNPAID',
      'badge--green': status === 'PAID',
      'badge--grey':  status === 'CANCELLED',
    };
  }

  getAddButtonTooltip(): string {
    if(this.articles.length< 1) return this.articles.length === 0 ? this.translate.instant('stock.responses.errors.ARTICLES_NOT_FOUND') : '';
    else if(this.clients.length< 1) return this.clients.length === 0 ? this.translate.instant('stock.responses.errors.CLIENTS_NOT_FOUND') : '';
    else return '';
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

  dismissError(): void { this.errors = []; }

  // ── Chart ─────────────────────────────────────────────────────────────────
  private getCSSVariable(variable: string, fallback = '#ffffff'): string {
    return getComputedStyle(document.documentElement).getPropertyValue(variable).trim() || fallback;
  }

  private renderStatusPieChart(): void {
    if (!this.monthlyChartRef || !this.invoiceStats) return;
    this.chart?.destroy();

    const textHi = this.getCSSVariable('--text-hi', '#ffffff');
    const labels = ['DRAFT', 'UNPAID', 'PAID', 'CANCELLED'].map(s =>
      this.translate.instant(`invoices.status.${s}`)
    );
    const data = [
      this.invoiceStats.draftCount,
      this.invoiceStats.unpaidCount,
      this.invoiceStats.paidCount,
      this.invoiceStats.cancelledCount,
    ];

    const config: ChartConfiguration = {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: ['#f5a623', '#e05252', '#3ecf8e', '#8b92a8'],
          borderColor: '#fff',
          borderWidth: 2,
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        plugins: {
          legend: {
            position: 'right',
            labels: { color: textHi, font: { family: 'Outfit, sans-serif', size: 12 }, usePointStyle: true, boxWidth: 10, padding: 15 },
          },
          tooltip: {
            callbacks: {
              label: ctx => {
                const total = data.reduce((a, b) => a + b, 0);
                const pct = total > 0 ? ((ctx.raw as number / total) * 100).toFixed(1) : 0;
                return `${ctx.label}: ${ctx.raw} (${pct}%)`;
              },
            },
          },
        },
      },
    };

    this.chart = new Chart(this.monthlyChartRef.nativeElement, config);
  }

  private observeThemeChanges(): void {
    this.themeObserver = new MutationObserver(() => {
      if (this.isStats() && this.invoiceStats) this.renderStatusPieChart();
    });
    this.themeObserver.observe(document.documentElement, {
      attributes: true, attributeFilter: ['class', 'data-theme'],
    });
  }
}