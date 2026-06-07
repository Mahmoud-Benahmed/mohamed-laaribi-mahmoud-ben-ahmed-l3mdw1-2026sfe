import { OnInit, OnDestroy, signal, computed, inject, DestroyRef, ViewChild, ElementRef, ChangeDetectorRef, Component} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, map, Observable, of } from 'rxjs';
import { RouterLink, Router } from '@angular/router';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import { SubscriptionPlanDto, TenantResponseDto } from '../../../interfaces/TenantDto';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { TenantService } from '../../../services/tenant/tenant.service';
import { SubscriptionPlanService } from '../../../services/tenant/subscription-plan.service';
import { PaginationComponent } from '../../pagination/pagination';
import { ModalComponent } from '../../modal/modal';

type ViewMode = 'list' | 'list-deleted' | 'stats' | 'list-suspended';

Chart.register(...registerables);

interface TenantStats {
  total: number;
  active: number;
  suspended: number;
  deleted: number;
}

interface TenantAnalyticsDto {
  totalTenants: number;
  activeTenants: number;
  suspendedTenants: number;
  deletedTenants: number;
  totalMRR: number;
  totalARR: number;
  averageUsagePercentage: number;
  topPlansByRevenue: Array<{ planName: string; revenue: number; tenantCount: number }>;
}

@Component({
  selector: 'app-tenants',
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
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class TenantsComponent implements OnInit, OnDestroy {
  @ViewChild('revenueChart') revenueChartRef!: ElementRef<HTMLCanvasElement>;

  private chart: Chart | null = null;
  private themeObserver: MutationObserver | null = null;
  private readonly destroyRef = inject(DestroyRef);
  private readonly translate = inject(TranslateService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  public readonly authService= inject(AuthService);

  readonly PRIVILEGES = PRIVILEGES;

  // ── View mode ──────────────────────────────────────────────────────────────
  viewMode = signal<ViewMode>('list');
  isList        = computed(() => this.viewMode() === 'list');
  isDeletedList = computed(() => this.viewMode() === 'list-deleted');
  isStats       = computed(() => this.viewMode() === 'stats');
  isSuspended   = computed(() => this.viewMode() === 'list-suspended');  // ← ADD

  // ── Tenant list state ──────────────────────────────────────────────────────
  loading: boolean= false;
  tenants: TenantResponseDto[] = [];
  deletedTenants: TenantResponseDto[] = [];
  suspendedTenants: TenantResponseDto[] = [];
  totalCount = 0;
  currentPage = 1;
  currentSize = 10;
  readonly pageSizeOptions = [5, 10, 25, 50];
  get totalPages(): number { return Math.ceil(this.totalCount / this.currentSize) || 1; }

  // ── Stats ──────────────────────────────────────────────────────────────────
  stats: TenantStats = { total: 0, active: 0, suspended: 0, deleted: 0 };
  tenantAnalytics: TenantAnalyticsDto | null = null;
  statsLoading = false;

  // ── Filters / sort ────────────────────────────────────────────────────────
  searchQuery = '';
  statusFilter = 'ALL';
  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  get sortedData(): TenantResponseDto[] {
  let data = this.isDeletedList()
    ? [...this.deletedTenants]
    : this.isSuspended()  // ← ADD
    ? [...this.suspendedTenants]  // ← ADD
    : [...this.tenants];

    if (this.searchQuery) {
      const q = this.searchQuery.toLowerCase();
      data = data.filter(t =>
        t.name.toLowerCase().includes(q) ||
        t.email.toLowerCase().includes(q) ||
        t.subdomainSlug.toLowerCase().includes(q)
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

  // ── Subscription plans ────────────────────────────────────────────────────
  subscriptionPlans: SubscriptionPlanDto[] = [];

  constructor(
    private tenantService: TenantService,
    private planService: SubscriptionPlanService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  ngAfterViewInit(): void {
    this.observeThemeChanges();
  }

  ngOnDestroy(): void {
    this.themeObserver?.disconnect();
    this.chart?.destroy();
  }

  // ── Load data ──────────────────────────────────────────────────────────────
  reload(): void {
    this.loading = true;

    // Load active tenants (for list display)
    this.tenantService.getAllTenants(this.currentPage, this.currentSize)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(err => {
          this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_FAILED'));
          return of({ items: [], totalCount: 0 });
        })
      )
      .subscribe(activeResult => {
        // Load deleted tenants count (for stats only, not for display)
        this.tenantService.getDeletedTenants(1, 100) // use page 1, size large enough to count all
          .pipe(
            takeUntilDestroyed(this.destroyRef),
            catchError(() => of({ items: [], totalCount: 0 }))
          )
          .subscribe(deletedResult => {
            // Load subscription plans
            this.planService.getAllPlans(1, 100)
              .pipe(
                takeUntilDestroyed(this.destroyRef),
                catchError(() => of({ items: [] }))
              )
              .subscribe(plansResult => {
                const allActive = activeResult.items.filter(t => !t.isDeleted);
                const deletedCount = deletedResult.totalCount;

                // Update stats BEFORE filtering displayed list
                this.stats = {
                  total: activeResult.totalCount + deletedCount,
                  active: allActive.filter(t => t.isActive).length,
                  suspended: allActive.filter(t => !t.isActive).length,
                  deleted: deletedCount,
                };

                // Set the displayed tenant list (all non-deleted tenants, both active and suspended)
                this.tenants = allActive;
                this.totalCount = activeResult.totalCount ?? 0;
                this.subscriptionPlans = plansResult.items || [];
                this.viewMode.set('list');
                this.loading = false;
                this.cdr.markForCheck();
              });
          });
      });
  }

  loadDeletedTenants(): void {
    this.tenantService.getDeletedTenants(this.currentPage, this.currentSize).subscribe({
      next: (res) => {
        this.deletedTenants = res.items.filter(t => t.isDeleted);
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_FAILED'));
      }
    });
  }

  loadSuspendedTenants(): void {
    this.tenantService.getAllTenants(this.currentPage, this.currentSize).subscribe({
      next: (res) => {
        this.suspendedTenants = res.items.filter(t => !t.isActive && !t.isDeleted);
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_FAILED'));
      }
    });
  }

  private loadCurrentView(): void {
    if (this.isDeletedList()) this.loadDeletedTenants();
    else if (this.isSuspended()) this.loadSuspendedTenants();
    else if (this.isStats()) this.loadAnalytics();
    else this.load();
  }

  load(): void {
    this.tenantService.getAllTenants(this.currentPage, this.currentSize).subscribe({
      next: res => {
        this.stats = {
          total: res.totalCount ?? 0,
          active: res.items.filter(t => !t.isDeleted && t.isActive).length,
          suspended: res.items.filter(t => !t.isDeleted && !t.isActive).length,
          deleted: res.items.filter(t => t.isDeleted).length,
        };
        this.tenants = res.items.filter(t => !t.isDeleted && t.isActive);
        this.totalCount = res.totalCount ?? 0;
        this.cdr.markForCheck();
      },
      error: () => {
        this.tenants = [];
        this.totalCount = 0;
        this.cdr.markForCheck();
        this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_FAILED'));
      },
    });
  }

  loadAnalytics(): void {
    this.statsLoading = true;
    this.tenantAnalytics = null;

    // Mock analytics call - replace with actual service if available
    this.tenantService.getAllTenants(1, 1000).subscribe({
      next: res => {
        const allTenants = res.items;
        const active = allTenants.filter(t => t.isActive && !t.isDeleted).length;
        const suspended = allTenants.filter(t => !t.isActive && !t.isDeleted).length;
        const deleted = allTenants.filter(t => t.isDeleted).length;

        this.tenantAnalytics = {
          totalTenants: allTenants.length,
          activeTenants: active,
          suspendedTenants: suspended,
          deletedTenants: deleted,
          totalMRR: allTenants.reduce((sum, t) => sum + (t.subscription?.plan?.monthlyPrice || 0), 0),
          totalARR: allTenants.reduce((sum, t) => sum + (t.subscription?.plan?.yearlyPrice || 0), 0),
          averageUsagePercentage: 65, // Mock value
          topPlansByRevenue: this.subscriptionPlans
            .map(p => ({
              planName: p.name,
              revenue: p.monthlyPrice * allTenants.filter(t => t.subscription?.plan?.id === p.id).length,
              tenantCount: allTenants.filter(t => t.subscription?.plan?.id === p.id).length,
            }))
            .sort((a, b) => b.revenue - a.revenue)
            .slice(0, 5),
        };

        this.statsLoading = false;
        setTimeout(() => this.renderRevenueChart(), 100);
      },
      error: () => {
        this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_ANALYTICS_FAILED'));
        this.statsLoading = false;
      },
    });
  }

  // ── Navigation ────────────────────────────────────────────────────────────
  openStats(): void {
    if (this.isStats()) return;
    this.viewMode.set('stats');
    this.loadAnalytics();
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
    if (this.isDeletedList()) this.loadDeletedTenants();
    else if (this.isSuspended()) this.loadSuspendedTenants();  // ← ADD
    else this.load();
  }

  onPageSizeChange(size: number): void {
    this.currentSize = size;
    this.currentPage = 1;
    if (this.isDeletedList()) this.loadDeletedTenants();
    else if (this.isSuspended()) this.loadSuspendedTenants();  // ← ADD
    else this.load();
  }

  // ── CRUD actions ───────────────────────────────────────────────────────────
  delete(tenant: TenantResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'delete', iconColor: 'warn',
        title: this.translate.instant('TENANTS.DIALOG.DELETE_TENANT_TITLE'),
        message: this.translate.instant('TENANTS.DIALOG.DELETE_TENANT_MESSAGE', { name: tenant.name }),
        confirmText: this.translate.instant('TENANTS.DIALOG.DELETE_CONFIRM'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });
    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;
      this.tenantService.deleteTenant(tenant.id).subscribe({
        next: () => {
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.DELETED'));
          this.isDeletedList() ? this.loadDeletedTenants() : this.reload();
        },
        error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.DELETE_FAILED')),
      });
    });
  }

  restore(tenant: TenantResponseDto): void {
    this.tenantService.restoreTenant(tenant.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('TENANTS.SUCCESS.RESTORED'));
        this.isDeletedList() ? this.loadDeletedTenants() : this.reload();
        this.isDeletedList() ? this.loadDeletedTenants() : this.reload();
      },
      error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.RESTORE_FAILED')),
    });
  }

  suspend(tenant: TenantResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'pause_circle',
        iconColor: 'warn',
        title: this.translate.instant('TENANTS.DIALOG.SUSPEND_TENANT_TITLE'),
        message: this.translate.instant('TENANTS.DIALOG.SUSPEND_TENANT_MESSAGE', { name: tenant.name }),
        confirmText: this.translate.instant('TENANTS.DIALOG.SUSPEND_CONFIRM'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });
    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;
      this.tenantService.removeSubscription(tenant.id).subscribe({
        next: () => {
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.SUSPENDED'));
          this.isDeletedList() ? this.loadDeletedTenants() : this.reload();
          this.isDeletedList() ? this.loadDeletedTenants() : this.reload();
        },
        error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.SUSPEND_FAILED')),
      });
    });
  }

  activate(tenant: TenantResponseDto): void {
    this.tenantService.activateTenant(tenant.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('TENANTS.SUCCESS.ACTIVATED'));
        this.isDeletedList() ? this.loadDeletedTenants() : this.reload();
      },
      error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.ACTIVATE_FAILED')),
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
  getStatusClass(tenant: TenantResponseDto): Record<string, boolean> {
    return {
      'badge--green': tenant.isActive && !tenant.isDeleted,
      'badge--amber': !tenant.isActive && !tenant.isDeleted,
      'badge--grey': tenant.isDeleted,
    };
  }

  getStatusLabel(tenant: TenantResponseDto): string {
    if (tenant.isDeleted) return 'TENANTS.STATUS.DELETED';
    return tenant.isActive ? 'TENANTS.STATUS.ACTIVE' : 'TENANTS.STATUS.SUSPENDED';
  }

  getPlanName(tenant: TenantResponseDto): string {
    return tenant.subscription?.plan?.name
      || this.translate.instant('TENANTS.NO_PLAN');
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

  // ── Chart ──────────────────────────────────────────────────────────────────
  private getCSSVariable(variable: string, fallback = '#ffffff'): string {
    return getComputedStyle(document.documentElement).getPropertyValue(variable).trim() || fallback;
  }

  private renderRevenueChart(): void {
    if (!this.revenueChartRef || !this.tenantAnalytics) return;
    this.chart?.destroy();

    const textHi = this.getCSSVariable('--text-hi', '#ffffff');
    const labels = this.tenantAnalytics.topPlansByRevenue.map(p => p.planName);
    const data = this.tenantAnalytics.topPlansByRevenue.map(p => p.revenue);

    const config: ChartConfiguration = {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: this.translate.instant('TENANTS.STATS.REVENUE_BY_PLAN'),
          data,
          backgroundColor: ['#3ecf8e', '#f5a623', '#e05252', '#6366f1', '#8b92a8'],
          borderRadius: 6,
          borderSkipped: false,
        }],
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: false,
          },
          tooltip: {
            callbacks: {
              label: ctx => {
                return `TND ${(ctx.raw as number).toFixed(2)}`;
              },
            },
          },
        },
        scales: {
          x: {
            ticks: { color: textHi },
            grid: { color: 'rgba(255,255,255,0.1)' },
          },
          y: {
            ticks: { color: textHi },
            grid: { display: false },
          },
        },
      },
    };

    this.chart = new Chart(this.revenueChartRef.nativeElement, config);
  }

  private observeThemeChanges(): void {
    this.themeObserver = new MutationObserver(() => {
      if (this.isStats() && this.tenantAnalytics) this.renderRevenueChart();
    });
    this.themeObserver.observe(document.documentElement, {
      attributes: true, attributeFilter: ['class', 'data-theme'],
    });
  }
}