import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, ValidatorFn, ValidationErrors, AbstractControl } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatTableDataSource } from '@angular/material/table';
import { TranslateService } from '@ngx-translate/core';
import {
  ClientsService, ClientResponseDto, ClientStatsDto,
  CreateClientRequestDto, UpdateClientRequestDto,
  AddCategoryRequestDto, AssignedCategoryDto
} from '../../../services/clients/clients.service';
import { ModalComponent } from '../../modal/modal';
import { PaginationComponent } from '../../pagination/pagination';
import { HttpError } from '../../../interfaces/HttpError';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { CategoriesService, ClientCategoryResponseDto } from '../../../services/clients/categories.service';
import { CurrencyConfigService } from '../../../services/currency-config.service';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';
import { InvoiceService } from '../../../services/invoice.service';
import { MatTooltip } from "@angular/material/tooltip";
import { RegexPatterns } from '../../../interfaces/RegexPatterns';

type ViewMode = 'list' | 'list-deleted' | 'list-blocked' | 'create' | 'edit' | 'view';

type CreditLimitInfo= {
    hasSufficientCredit: boolean,
    currentUsage: number,
    remainingCredit: number
};

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIcon, PaginationComponent, TranslatePipe, MatTooltip],
  templateUrl: './home.html',
  styleUrls: ['./home.scss'],
})
export class ClientsComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly location= inject(Location);
  private translate = inject(TranslateService);

  dataSource = new MatTableDataSource<ClientResponseDto>([]);

  categories: ClientCategoryResponseDto[] = [];
  stats: ClientStatsDto | null = null;

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  creditLimitInfo: CreditLimitInfo = {
    hasSufficientCredit: true,
    currentUsage: 0,
    remainingCredit: 0
  };

  creditRemaining: number | null = null;
  outstandingBalance: number = 0;
  // ── Signals ───────────────────────────────────────────────────────────────

  viewMode = signal<ViewMode>('list');
  isMode = (mode: ViewMode) => computed(() => this.viewMode() === mode);

  isList        = this.isMode('list');
  isDeletedList = this.isMode('list-deleted');
  isBlockedList = this.isMode('list-blocked');
  isCreate      = this.isMode('create');
  isEdit        = this.isMode('edit');
  isView        = this.isMode('view');

  private previousMode: ViewMode = 'list';

  clientIdFromRoute: string | null = null;
  selectedClient: ClientResponseDto | null = null;
  loading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  searchQuery = '';

  readonly PRIVILEGES = PRIVILEGES;
  clientForm: FormGroup;

  sortColumn: string = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  selectedCategoryId = '';

  constructor(
    public authService: AuthService,
    private clientsService: ClientsService,
    private categoriesService: CategoriesService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    private currencyConfig: CurrencyConfigService,
    private route: ActivatedRoute,
    private invoiceService: InvoiceService,
  ) {
    this.clientForm = this.fb.group({
      name:             ['', [Validators.required, Validators.pattern(RegexPatterns.alpha), Validators.minLength(2), Validators.maxLength(200)]],
      email:            ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
      address:          ['', [Validators.required, Validators.pattern(RegexPatterns.safeText), Validators.minLength(5), Validators.maxLength(500)]],
      phone:            ['', [Validators.maxLength(20), Validators.pattern(RegexPatterns.phone)]],
      taxNumber:        ['', [Validators.maxLength(50), Validators.pattern(RegexPatterns.alphaNumeric)]],
      creditLimit:      [null, this.optionalMin(1000)],
      duePaymentPeriod: [null, this.optionalRange(7, 180)],
      delaiRetour:      [null, this.optionalRange(7, 270)],
    });
  }

  ngOnInit(): void {
    this.dataSource.filterPredicate = (data, filter) =>
      this.flattenObject(data).includes(filter);

    this.clientIdFromRoute = this.route.snapshot.paramMap.get('id');

    if(this.clientIdFromRoute == null){
      this.reload();
    }else{
      this.clientsService.getById(this.clientIdFromRoute).subscribe({
        next: (client) => {
          this.selectedClient = client;
          this.loadCreditLimitInfo();
          this.setViewMode('view');

          this.loadCategories();
          this.loadStats();

          this.cdr.markForCheck();
        },
        error: () => {
          this.flash('error', this.translate.instant('clients.responses.errors.CLIENT_NOT_FOUND'));
          this.setViewMode('list');
          this.reload();
        }
      });
    }
  }

  // ── Page title ────────────────────────────────────────────────────────────

  get pageTitle(): string {
    if (this.isCreate())      return 'clients.title_add';
    if (this.isEdit())        return 'clients.title_edit';
    if (this.isView())        return 'clients.title_details';
    if (this.isDeletedList()) return 'clients.title_deleted';
    if (this.isBlockedList()) return 'clients.title_blocked';
    return 'clients.title_list';
  }

  private translateError(errorCode: string): string {
    const clientKey = `clients.responses.errors.${errorCode}`;
    if (this.translate.instant(clientKey) !== clientKey) {
      return this.translate.instant(clientKey);
    }
    const categoryKey = `clients.categories.responses.errors.${errorCode}`;
    if (this.translate.instant(categoryKey) !== categoryKey) {
      return this.translate.instant(categoryKey);
    }
    return this.translate.instant('errors.unknown');
  }

  // ── Stats ─────────────────────────────────────────────────────────────────

  get totalClients():   number { return this.stats?.totalClients   ?? 0; }
  get activeClients():  number { return this.stats?.activeClients  ?? 0; }
  get blockedClients(): number { return this.stats?.blockedClients ?? 0; }
  get deletedClients(): number { return this.stats?.deletedClients ?? 0; }

  get currencyCode():   string { return this.currencyConfig.code;   }
  get currencyLocale(): string { return this.currencyConfig.locale; }

  // ── Sorting ───────────────────────────────────────────────────────────────

  get sortedData(){
    const data = [...this.dataSource.filteredData];

    if (!this.sortColumn) return data;

    return data.sort((a, b) => {
      let valA = this.getNestedValue(a, this.sortColumn);
      let valB = this.getNestedValue(b, this.sortColumn);

      if (valA == null) return 1;
      if (valB == null) return -1;

      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();

      return (valA < valB ? -1 : valA > valB ? 1 : 0) *
        (this.sortDirection === 'asc' ? 1 : -1);
    });
  }

  sortBy(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
  }

  applyFilter(): void {
    this.dataSource.filter = this.searchQuery.trim().toLowerCase();
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }
  onPageSizeChange(): void { this.pageNumber.set(1); this.load(); }

  // ── Load (pure fetchers — no mode switching) ──────────────────────────────

  load(): void {
    this.errors = [];
    this.clientsService.getAll(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items.filter(c => !c.isBlocked && !c.isDeleted);
        this.totalCount = res.totalCount;
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.unknown')),
    });
  }

  loadDeleted(): void {
    this.clientsService.getDeleted(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items;
        this.totalCount = res.totalCount;
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.unknown')),
    });
  }

  loadBlocked(): void {
    this.clientsService.getAll(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items.filter(c => c.isBlocked);
        this.totalCount = res.totalCount;
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.unknown')),
    });
  }

  loadCategories(): void {
    this.categoriesService.getAll().subscribe({
      next: (cats) => { this.categories = cats; this.cdr.markForCheck(); },
      error: () => this.flash('error', this.translate.instant('errors.unknown'))
    });
  }

  loadStats(): void {
    this.clientsService.getStats().subscribe({
      next: (res) => {
        this.stats = res;
        if (this.isDeletedList() && res.deletedClients === 0) {
          this.setViewMode('list');
          this.load();
        }
        if (this.isBlockedList() && res.blockedClients === 0) {
          this.setViewMode('list');
          this.load();
        }
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.unknown')),
    });
  }

  reload(): void {
    forkJoin({
      clients: this.clientsService.getAll(this.pageNumber(), this.pageSize()),
      stats: this.clientsService.getStats(),
      categories: this.categoriesService.getAll()
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ clients, stats, categories }) => {
        this.dataSource.data = clients.items.filter(c => !c.isBlocked && !c.isDeleted);
        this.totalCount = clients.totalCount;
        this.stats = stats;
        this.categories = categories;
        this.loadCreditLimitInfo();
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.unknown'))
    });
  }

  // ── Stat card clicks ──────────────────────────────────────────────────────

  onActiveCardClick(): void {
    if (this.isList()) return;
    this.setViewMode('list');
    this.load();
  }

  onBlockedCardClick(): void {
    if (this.isBlockedList() || this.blockedClients < 1) return;
    this.setViewMode('list-blocked');
    this.loadBlocked();
  }

  onDeletedCardClick(): void {
    if (this.isDeletedList() || this.deletedClients < 1) return;
    this.setViewMode('list-deleted');
    this.loadDeleted();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  openCreate(): void {
    if (this.isCreate()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('create');
    this.selectedClient = null;
    this.clientForm.reset({
      name: '', email: '', address: '',
      phone: '', taxNumber: '', creditLimit: null,
      delaiRetour: null, duePaymentPeriod: null
    });
  }

  openView(client: ClientResponseDto): void {
    if (this.isView()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('view');
    this.selectedClient = client;
    this.loadCreditLimitInfo();
    this.selectedCategoryId = '';
    this.cdr.markForCheck();
  }

  openEdit(client: ClientResponseDto): void {
    if (this.isEdit()) return;
    this.previousMode = this.viewMode();
    this.selectedClient = client;
    this.setViewMode('edit');

    this.clientForm.patchValue({
      name:             client.name,
      email:            client.email,
      address:          client.address,
      phone:            client.phone ?? '',
      taxNumber:        client.taxNumber ?? '',
      creditLimit:      client.creditLimit ?? null,
      duePaymentPeriod: client.duePaymentPeriod > 0 ? client.duePaymentPeriod : null,
      delaiRetour:      client.delaiRetour && client.delaiRetour > 0 ? client.delaiRetour : null,
    });

    this.cdr.markForCheck();
  }

  cancel(): void {
    if(this.clientIdFromRoute) this.location.back();
    const target = this.resolveCancel();
    const needsClient: ViewMode[] = ['view', 'edit'];

    this.setViewMode(target);

    if (!needsClient.includes(target)) {
      this.selectedClient = null;
      this.selectedCategoryId = '';
    }

    if (target !== 'edit') {
      this.clientForm.reset();
    }
  }

  private resolveCancel(): ViewMode {
    const current = this.viewMode();

    if (current === 'edit' && this.previousMode === 'view' && this.selectedClient) {
      return 'view';
    }

    if (current === 'view' && (
      this.previousMode === 'list' ||
      this.previousMode === 'list-deleted' ||
      this.previousMode === 'list-blocked'
    )) {
      this.reloadForMode(this.previousMode);
      return this.previousMode;
    }

    if (current === 'create') {
      this.reloadForMode(this.previousMode ?? 'list');
      return this.previousMode ?? 'list';
    }

    this.load();
    return 'list';
  }

  private reloadForMode(mode: ViewMode): void {
    if (mode === 'list-deleted') {
      this.loadDeleted();
    } else if (mode === 'list-blocked') {
      this.loadBlocked();
    } else {
      this.load();
    }
    this.loadStats();
  }

  restore(client: ClientResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant('clients.confirmations.restore_client.title'),
        message:     this.translate.instant('clients.confirmations.restore_client.message', { name: client.name }),
        confirmText: this.translate.instant('common.restore'),
        showCancel:  true,
        icon:        'settings_backup_restore',
        iconColor:   'success',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        if (!result) return;
        this.clientsService.restore(client.id).subscribe({
          next: () => {
            if (this.isView()) this.cancel();
            this.flash('success', this.translate.instant('clients.responses.success.client_restored', { name: client.name }));
            this.reload();
          },
          error: () => this.flash('error', this.translate.instant('errors.unknown')),
        });
      });
  }

  submit(): void {
    if (this.clientForm.invalid) return;
    const val = this.clientForm.value;

    if (this.isCreate()) {
      const dto: CreateClientRequestDto = {
        name:             val.name,
        email:            val.email,
        address:          val.address,
        phone:            val.phone || undefined,
        taxNumber:        val.taxNumber || undefined,
        creditLimit:      val.creditLimit ?? undefined,
        delaiRetour:      val.delaiRetour ?? undefined,
        duePaymentPeriod: val.duePaymentPeriod ?? undefined,
      };
      this.clientsService.create(dto).subscribe({
        next: () => { this.cancel(); this.reload(); this.flash('success', this.translate.instant('clients.responses.success.client_created', { name: val.name })); },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('errors.unknown')),
      });
    } else if (this.isEdit() && this.selectedClient) {
      const dto: UpdateClientRequestDto = {
        name:             val.name,
        email:            val.email,
        address:          val.address,
        phone:            val.phone,
        taxNumber:        val.taxNumber,
        creditLimit:      val.creditLimit,
        delaiRetour:      val.delaiRetour,
        duePaymentPeriod: val.duePaymentPeriod,
      };
      this.clientsService.update(this.selectedClient.id, dto).subscribe({
        next: () => { this.cancel(); this.reload(); this.flash('success', this.translate.instant('clients.responses.success.client_updated', { name: val.name })); },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('errors.unknown')),
      });
    }
  }

  delete(client: ClientResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant('clients.confirmations.delete_client.title'),
        message:     this.translate.instant('clients.confirmations.delete_client.message', { name: client.name }),
        confirmText: this.translate.instant('common.delete'),
        showCancel:  true,
        icon:        'auto_delete',
        iconColor:   'danger',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        this.clientsService.delete(client.id).subscribe({
          next: () => {
            if (this.isView()) this.cancel();
            this.flash('success', this.translate.instant('clients.responses.success.client_deleted', { name: client.name }));
            this.reload();
          },
          error: () => this.flash('error', this.translate.instant('errors.unknown')),
        });
      });
  }

  toggleBlock(client: ClientResponseDto): void {
    const isBlocked = client.isBlocked;
    const confirmationKey = isBlocked ? 'unblock_client' : 'block_client';
    const successKey = isBlocked ? 'client_unblocked' : 'client_blocked';

    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant(`clients.confirmations.${confirmationKey}.title`),
        message:     this.translate.instant(`clients.confirmations.${confirmationKey}.message`, { name: client.name }),
        confirmText: this.translate.instant(isBlocked ? 'common.unblock' : 'common.block'),
        showCancel:  true,
        icon:        isBlocked ? 'lock_open' : 'block',
        iconColor:   isBlocked ? 'success' : 'warning',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        this.clientsService.toggleBlock(client).subscribe({
          next: (updated) => {
            this.flash('success', this.translate.instant(`clients.responses.success.${successKey}`, { name: client.name }));
            if (this.selectedClient?.id === client.id) this.selectedClient = updated;
            this.reload();
          },
          error: () => this.flash('error', this.translate.instant('errors.unknown')),
        });
      });
  }

  // ── Category Management ───────────────────────────────────────────────────

  addCategory(clientId: string): void {
    if (!this.selectedCategoryId) return;
    const dto: AddCategoryRequestDto = { categoryId: this.selectedCategoryId };

    this.clientsService.addCategory(clientId, dto).subscribe({
      next: (result) => {
        this.selectedCategoryId = '';
        this.flash('success', this.translate.instant('clients.responses.success.client_category_added'));
        this.selectedClient = result; this.reload();
      },
      error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('errors.unknown')),
    });
  }

  removeCategory(clientId: string, categoryId: string, categoryName: string): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant('clients.confirmations.remove_category.title'),
        message:     this.translate.instant('clients.confirmations.remove_category.message', { name: categoryName }),
        confirmText: this.translate.instant('common.remove'),
        showCancel:  true,
        icon:        'label_off',
        iconColor:   'danger',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        this.clientsService.removeCategory(clientId, categoryId).subscribe({
          next: (client) => {
            this.flash('success', this.translate.instant('clients.responses.success.client_category_removed', { name: categoryName }));
            this.selectedClient = client;
            this.reload();
          },
          error: () => this.flash('error', this.translate.instant('errors.unknown')),
        });
      });
  }

  get availableCategories(): ClientCategoryResponseDto[] {
    if (!this.selectedClient) return this.categories;
    return this.categories.filter(
      cat => !this.clientsService.hasCategory(this.selectedClient!, cat.id)
    );
  }

  // ── Feedback ──────────────────────────────────────────────────────────────

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; this.cdr.markForCheck(); }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; this.cdr.markForCheck(); }, 4000);
    }
    this.cdr.markForCheck();
  }

  dismissError(): void { this.errors = []; }

  // ── Helpers ───────────────────────────────────────────────────────────────

  trackById(_: number, c: ClientResponseDto): string { return c.id; }
  trackByCategoryId(_: number, cat: AssignedCategoryDto): string { return cat.id; }

  getCategoryNames(client: ClientResponseDto): string {
    return this.clientsService.getCategoryNames(client);
  }

  private flattenObject(obj: any): string {
    return Object.keys(obj)
      .map(key => {
        const value = obj[key];
        if (value && typeof value === 'object') return this.flattenObject(value);
        return value;
      })
      .join(' ')
      .toLowerCase();
  }

  private getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((acc, key) => acc?.[key], obj);
  }

  setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
    this.cdr.markForCheck();
  }

  loadCreditLimitInfo(): void {
    if (!this.selectedClient?.id) {
      this.creditRemaining = null;
      this.outstandingBalance = 0;
      return;
    }

    if (!this.selectedClient.effectiveCreditLimit) {
      this.creditRemaining = null;
      this.outstandingBalance = 0;
      return;
    }

    this.invoiceService.getClientOutstandingBalance(this.selectedClient.id).subscribe({
      next: (outstanding) => {
        this.outstandingBalance = outstanding;
        this.creditRemaining = Math.max(0, this.selectedClient!.effectiveCreditLimit! - outstanding);
        this.cdr.markForCheck();
      },
      error: () => {
        this.outstandingBalance = 0;
        this.creditRemaining = null;
        this.cdr.markForCheck();
      }
    });
  }

  getSelectedClientDiscountRate(): number {
    if(!this.selectedClient) return 0;
    if (!this.selectedClient?.categories?.length) return 0;
    const rates = this.selectedClient.categories
      .map(c => c.discountRate)
      .filter((rate): rate is number => rate !== null && rate !== undefined);
    return rates.length > 0 ? Math.max(...rates) * 100 : 0;
  }

  optionalMin(minValue: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (val === null || val === undefined || val === '') return null;
      const num = Number(val);
      if (isNaN(num)) return { min: true };
      return num >= minValue ? null : { min: true };
    };
  }

  optionalPositive(): ValidatorFn {
    return this.optionalMin(0.01);
  }

  optionalRange(min: number, max: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (val === null || val === undefined || val === '') return null;
      const num = Number(val);
      if (isNaN(num)) return { range: true };
      if (num < min) return { min: { min, actual: num } };
      if (num > max) return { max: { max, actual: num } };
      return null;
    };
  }
}