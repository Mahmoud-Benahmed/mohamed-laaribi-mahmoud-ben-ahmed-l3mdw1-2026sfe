import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatTableDataSource } from '@angular/material/table';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { ModalComponent } from '../../modal/modal';
import { PaginationComponent } from '../../pagination/pagination';
import { HttpError } from '../../../interfaces/HttpError';
import { CategoriesService, CategoryStatsDto, CreateCategoryRequestDto, UpdateCategoryRequestDto } from '../../../services/clients/categories.service';
import { ClientCategoryResponseDto } from '../../../services/clients/categories.service';
import { CustomToggleComponent } from '../../toggle-slider/toggle-slider';
import { ArticleCategoryResponseDto } from '../../../services/articles/categories.service';
import { TranslatePipe } from '@ngx-translate/core';
import { ActivatedRoute } from '@angular/router';
import { RegexPatterns } from '../../../interfaces/RegexPatterns';

type ViewMode = 'list' | 'create' | 'edit' | 'view' | 'list-deleted' | 'list-inactive';

@Component({
  selector: 'app-client-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIcon, PaginationComponent, CustomToggleComponent, TranslatePipe],
  templateUrl: './categories.html',
  styleUrls: ['./categories.scss'],
})
export class ClientCategoriesComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly location= inject(Location);

  dataSource = new MatTableDataSource<ClientCategoryResponseDto>([]);
  stats: CategoryStatsDto | null = null;

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  // ── Signals ───────────────────────────────────────────────────────────────

  viewMode = signal<ViewMode>('list');
  isMode = (mode: ViewMode) => computed(() => this.viewMode() === mode);

  isList   = this.isMode('list');
  isListDeleted   = this.isMode('list-deleted');
  isInactiveList = this.isMode('list-inactive');
  isCreate = this.isMode('create');
  isEdit   = this.isMode('edit');
  isView   = this.isMode('view');

  private previousMode: ViewMode = 'list';

  selectedCategory: ClientCategoryResponseDto | null = null;
  categoryIdFromRoute: string | null = null;

  loading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  searchQuery = '';

  readonly PRIVILEGES = PRIVILEGES;
  categoryForm: FormGroup;

  sortColumn: string = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  constructor(
    public authService: AuthService,
    private categoriesService: CategoriesService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    private route: ActivatedRoute
  ) {
    this.categoryForm = this.fb.group({
      name:                  ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200), Validators.pattern(RegexPatterns.alpha)]],
      code:                  ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50), Validators.pattern(RegexPatterns.categoryCode)]],
      delaiRetour:           [null, [Validators.required, Validators.min(7), Validators.max(270), Validators.pattern(RegexPatterns.integer)]],
      duePaymentPeriod:      [null, [Validators.required, Validators.min(7), Validators.max(180), Validators.pattern(RegexPatterns.integer)]],
      discountRate:          [null, [Validators.min(0), Validators.max(100)]],
      creditLimitMultiplier: [null, [Validators.min(1), Validators.max(2)]],
      useBulkPricing:        [false],
    });

    this.categoryForm.get('useBulkPricing')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(useBulk => {
        const discountControl = this.categoryForm.get('discountRate');
        if (useBulk) {
          discountControl?.enable();
          discountControl?.setValidators([Validators.required, Validators.min(0), Validators.max(100)]);
        } else {
          discountControl?.setValue(0, {emitEvent: false});
          discountControl?.disable();
          discountControl?.clearValidators();
        }
        discountControl?.updateValueAndValidity();
    });
  }

  ngOnInit(): void {
    this.dataSource.filterPredicate = (data, filter) => this.flattenObject(data).includes(filter);

    this.categoryIdFromRoute = this.route.snapshot.paramMap.get('id');
    if(this.categoryIdFromRoute == null){
      this.reload();
    }else{
      this.categoriesService.getById(this.categoryIdFromRoute).subscribe({
        next: (res)=>{
          this.selectedCategory= res;
          this.setViewMode('view');
          this.reload();
        },
        error: (error)=>{
          const err= error.error as HttpError;
          this.flash("error", err.message);
          this.cancel();
        }
      });
    }
  }

  // ── Page title ────────────────────────────────────────────────────────────

  get pageTitle(): string {
    if (this.isCreate()) return 'Add Category';
    if (this.isEdit())   return 'Edit Category';
    if (this.isView())   return 'Category Details';
    return 'Client Categories';
  }

  // ── Stats ─────────────────────────────────────────────────────────────────

  get totalCategories():    number { return this.stats?.totalCategories    ?? 0; }
  get activeCategories():   number { return this.stats?.activeCategories   ?? 0; }
  get inactiveCategories(): number { return this.stats?.inactiveCategories ?? 0; }
  get deletedCategories():  number { return this.stats?.deletedCategories  ?? 0; }

  // ── Sorting ───────────────────────────────────────────────────────────────

  sortBy(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
  }

  get sortedData(): ClientCategoryResponseDto[] {
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

  applyFilter(): void {
    this.dataSource.filter = this.searchQuery.trim().toLowerCase();
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }
  onPageSizeChange(): void { this.pageNumber.set(1); this.load(); }

  // ── Load (pure fetchers) ──────────────────────────────────────────────────

  load(): void {
    this.loading = true;
    this.errors = [];
    this.categoriesService.getAllPaged(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items.filter(c => c.isActive);
        this.totalCount = res.totalCount;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', 'Failed to load categories.');
        this.loading = false;
      },
    });
  }

  listDeleted(): void {
    this.categoriesService.getDeleted(this.pageNumber(), this.pageSize()).subscribe({
      next: (result) => {
        this.dataSource.data = result.items;
        this.totalCount = result.totalCount;
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', 'Failed to load deleted categories.'),
    });
  }

  loadStats(): void {
    this.categoriesService.getStats().subscribe({
      next: (res) => { this.stats = res; this.cdr.markForCheck(); },
      error: () => this.flash('error', 'Failed to load stats.'),
    });
  }


  loadInactive(): void {
    this.categoriesService.getAllPaged(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items.filter(c => !c.isActive && !c.isDeleted);
        this.totalCount = res.totalCount;
        this.cdr.markForCheck();
      },
      error: (error) => this.flash('error', (error as HttpError).message || 'Failed to load categories.'),
    });
    this.cdr.markForCheck();
  }

  reload(): void {
    this.load();
    this.loadStats();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  openCreate(): void {
    if (this.isCreate()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('create');
    this.selectedCategory = null;
    this.categoryForm.reset({
      name: '', code: '', delaiRetour: null, duePaymentPeriod: null,
      useBulkPricing: false, discountRate: null, creditLimitMultiplier: null,
    });
  }

  openEdit(category: ClientCategoryResponseDto): void {
    if (this.isEdit()) return;
    this.previousMode = this.viewMode();
    this.selectedCategory = category;
    this.setViewMode('edit');
    this.categoryForm.patchValue({
      name:                  category.name,
      code:                  category.code,
      delaiRetour:           category.delaiRetour,
      duePaymentPeriod:      category.duePaymentPeriod,    // ← added
      useBulkPricing:        category.useBulkPricing,
      discountRate: category.discountRate != null ? category.discountRate * 100 : null,
      creditLimitMultiplier: category.creditLimitMultiplier ?? null,
    });
    this.cdr.markForCheck();
  }

  openView(category: ClientCategoryResponseDto): void {
    if (this.isView()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('view');
    this.selectedCategory = category;
    this.cdr.markForCheck();
  }

  cancel(): void {
    if(this.categoryIdFromRoute) this.location.back();

    const target = this.resolveCancel();
    const needsCategory: ViewMode[] = ['view', 'edit'];

    this.setViewMode(target);

    if (!needsCategory.includes(target)) {
      this.selectedCategory = null;
    }

    if (target !== 'edit') {
      this.categoryForm.reset();
    }
  }

  private resolveCancel(): ViewMode {
    const current = this.viewMode();

    // edit → view: only go back to view if selectedCategory is still available
    if (current === 'edit' && this.previousMode === 'view' && this.selectedCategory) {
      return 'view';
    }

    // view → list / list-deleted: go back to wherever list was
    if (current === 'view' && (this.previousMode === 'list' || this.previousMode === 'list-deleted')) {
      return this.previousMode;
    }

    // create → list: always safe
    if (current === 'create') {
      return this.previousMode ?? 'list';
    }

    // fallback
    return 'list';
  }

  onActiveCardClick(): void {
    if (this.isList()) return;
    this.setViewMode('list');
    this.load();
  }

  onDeletedCardClick(): void {
    if (this.isListDeleted() || this.deletedCategories < 1) return;
    this.setViewMode('list-deleted');
    this.listDeleted();
  }

  onInactiveCardClick(): void {
    if (this.isInactiveList() || this.inactiveCategories < 1) return;
    this.setViewMode('list-inactive');
    this.loadInactive();
  }

  submit(): void {
    if (this.categoryForm.invalid) return;
    const val = this.categoryForm.value;

    if (this.isCreate()) {
      const dto: CreateCategoryRequestDto = {   // same shape for UpdateCategoryRequestDto
        name:                  val.name,
        code:                  val.code,
        delaiRetour:           val.delaiRetour,
        duePaymentPeriod:      val.duePaymentPeriod,          // ← added
        useBulkPricing:        val.useBulkPricing ?? false,
        discountRate: val.discountRate != null && val.discountRate !== '' ? val.discountRate / 100 : null,
        creditLimitMultiplier: val.creditLimitMultiplier ?? null,
      };
      this.categoriesService.create(dto).subscribe({
        next: () => { this.cancel(); this.reload(); this.flash('success', `Category "${val.name}" created successfully.`); },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? 'Failed to create category.'),
      });
    } else if (this.isEdit() && this.selectedCategory) {
      const dto: UpdateCategoryRequestDto = {   // same shape for UpdateCategoryRequestDto
        name:                  val.name,
        code:                  val.code,
        delaiRetour:           val.delaiRetour,
        duePaymentPeriod:      val.duePaymentPeriod,          // ← added
        useBulkPricing:        val.useBulkPricing ?? false,
        discountRate:          val.discountRate != null && val.discountRate !== '' ? val.discountRate / 100 : null,
        creditLimitMultiplier: val.creditLimitMultiplier ?? null,
      };
      this.categoriesService.update(this.selectedCategory.id, dto).subscribe({
        next: () => { this.cancel(); this.reload(); this.flash('success', `Category "${val.name}" updated successfully.`); },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? 'Failed to update category.'),
      });
    }
  }

  delete(category: ClientCategoryResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       'Delete Category',
        message:     `Category "${category.name}" will be soft-deleted. Clients assigned to it will not be affected. Proceed?`,
        confirmText: 'Delete',
        showCancel:  true,
        icon:        'auto_delete',
        iconColor:   'danger',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        this.categoriesService.delete(category.id).subscribe({
          next: () => {
            if (this.isView()) this.cancel();
            this.flash('success', `Category "${category.name}" deleted successfully.`);
            this.reload();
          },
          error: (error) =>
          {
            const err= error.error as HttpError;
            this.flash('error', err.message);
          },
        });
      });
  }



  restore(cat: ClientCategoryResponseDto): void {
      this.categoriesService.restore(cat.id).subscribe({
        next: () => {
          this.flash('success', `ClientCategoryResponseDto "${cat.name}" has been restored. You can find it in the Categories page.`);
          this.reload();
          if(this.isView())this.cancel();
        },
        error: (error) =>{
          const err= error.error as HttpError;
          this.flash('error', error.message);
        }
      });
  }


  // ── Activate / Deactivate ─────────────────────────────────────────────────

  toggleActive(category: ClientCategoryResponseDto): void {
    const action = category.isActive ? 'Deactivate' : 'Activate';
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       `${action} Category`,
        message:     `Are you sure you want to ${action.toLowerCase()} "${category.name}"?`,
        confirmText: action,
        showCancel:  true,
        icon:        category.isActive ? 'toggle_off' : 'toggle_on',
        iconColor:   category.isActive ? 'warning' : 'success',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        const call = category.isActive
          ? this.categoriesService.deactivate(category.id)
          : this.categoriesService.activate(category.id);

        call.subscribe({
          next: (updated) => {
            this.flash('success', `Category "${category.name}" ${action.toLowerCase()}d successfully.`);
            if (this.selectedCategory?.id === category.id) this.selectedCategory = updated;
            this.reload();
          },
          error: () => this.flash('error', `Failed to ${action.toLowerCase()} category.`),
        });
      });
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

  trackById(_: number, c: ClientCategoryResponseDto): string { return c.id; }

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
}
