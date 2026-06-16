import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';

import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { ModalComponent } from '../../modal/modal';
import { PaginationComponent } from '../../pagination/pagination';
import { HttpError } from '../../../interfaces/HttpError';
import { CategoryRequestDto, ArticleCategoryResponseDto, CategoryService, ArticleCategoryStatsDto } from '../../../services/articles/categories.service';
import { MatTableDataSource } from '@angular/material/table';
import { TranslatePipe } from '@ngx-translate/core';
import { RegexPatterns } from '../../../interfaces/RegexPatterns';
import { HttpErrorResponse } from '@angular/common/http';

type ViewMode = 'list' | 'list-deleted' | 'create' | 'edit' | 'view';

@Component({
  selector: 'app-article-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIcon, PaginationComponent, TranslatePipe],
  templateUrl: './categories.html',
  styleUrls: ['./categories.scss'],
})
export class ArticleCategoriesComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);

  dataSource = new MatTableDataSource<ArticleCategoryResponseDto>([]);
  stats: ArticleCategoryStatsDto | null = null;

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  private previousMode: ViewMode = 'list';

  selectedCategory: ArticleCategoryResponseDto | null = null;
  loading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  searchQuery = '';

  viewMode = signal<ViewMode>('list');
  isMode = (mode: ViewMode) => computed(() => this.viewMode() === mode);

  isList        = this.isMode('list');
  isDeletedList = this.isMode('list-deleted');
  isCreate      = this.isMode('create');
  isEdit        = this.isMode('edit');
  isView        = this.isMode('view');

  readonly PRIVILEGES = PRIVILEGES;
  categoryForm: FormGroup;

  readonly templateTranslationKey= `articles.categories`;
  readonly responseSuccessTranslationKey="articles.responses.success";
  readonly confirmationsTranslationKey="articles.confirmations";

  constructor(
    public authService: AuthService,
    private categoryService: CategoryService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef
  ) {
    this.categoryForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100), Validators.pattern(RegexPatterns.safeText)]],
      tva:  [null, [Validators.required, Validators.min(0), Validators.max(100), Validators.pattern(RegexPatterns.integer)]],
    });
  }

  ngOnInit(): void {
    this.reload();
  }

  // ── Page title ────────────────────────────────────────────────────────────

  get pageTitle(): string {
    if (this.isCreate())      return `${this.templateTranslationKey}.title_add`;
    if (this.isEdit())        return `${this.templateTranslationKey}.title_edit`;
    if (this.isView())        return `${this.templateTranslationKey}.title_details`;
    if (this.isDeletedList()) return `${this.templateTranslationKey}.title_deleted`;
    return `${this.templateTranslationKey}.title_deleted`;
  }

  private translateError(errorCode: string): string {
    const key = `articles.responses.errors.${errorCode}`;
    const translated = this.translate.instant(key);
    if (translated !== key) {
      return translated;
    }
    return this.translate.instant('errors.internal_error');
  }

  // ── Search ────────────────────────────────────────────────────────────────

  applyFilter(): void {
    this.dataSource.filter = this.searchQuery.trim().toLowerCase();
    this.pageNumber.set(1);
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }

  onPageSizeChange(): void { this.pageNumber.set(1); this.load(); }

  get activeCategories():  number { return this.stats?.activeCategories  ?? 0; }
  get deletedCategories(): number { return this.stats?.deletedCategories ?? 0; }

  // ── Sort ──────────────────────────────────────────────────────────────────

  sortBy(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
  }

  get sortedData() {
    const data = [...this.dataSource.filteredData];
    if (!this.sortColumn) return data;

    return data.sort((a, b) => {
        let valA = (a as any)[this.sortColumn];
        let valB = (b as any)[this.sortColumn];

        if (valA == null) return 1;
        if (valB == null) return -1;

        if (typeof valA === 'string') valA = valA.toLowerCase();
        if (typeof valB === 'string') valB = valB.toLowerCase();

        return (valA < valB ? -1 : valA > valB ? 1 : 0) * (this.sortDirection === 'asc' ? 1 : -1);
    });
  }

  // ── Load ──────────────────────────────────────────────────────────────────

  load(): void {
    this.errors = [];
    this.categoryService.getPaged(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items;
        this.totalCount = res.totalCount;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
        this.flash('error', msg);
      },
    });
  }

  listDeleted(): void {
    this.categoryService.getDeleted(this.pageNumber(), this.pageSize()).subscribe({
      next: (result) => {
        this.dataSource.data = result.items;
        this.totalCount = result.totalCount;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
        this.flash('error', msg);
      },
    });
  }

  loadStats(): void {
    this.categoryService.getStats().subscribe({
      next: (res) => {
        this.stats = res;
        // auto-switch back to list when no deleted items remain
        if (this.isDeletedList() && res.deletedCategories === 0) {
          this.setViewMode('list');
          this.load();
        }
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
        this.flash('error', msg);
      },
    });
  }

  reload(): void {
    if (this.isDeletedList()) {
      this.listDeleted();
    } else {
      this.load();
    }
    this.loadStats();
    this.cdr.markForCheck();
  }

  // ── Stat card clicks ──────────────────────────────────────────────────────

  onActiveCardClick(): void {
    if (this.isList()) return;
    this.setViewMode('list');
    this.load();
  }

  onDeletedCardClick(): void {
    if (this.isDeletedList() || this.deletedCategories < 1) return;
    this.setViewMode('list-deleted');
    this.listDeleted();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  openCreate(): void {
    if (this.isCreate()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('create');
    this.selectedCategory = null;
    this.categoryForm.reset({ name: '', tva: null });
  }

  openEdit(category: ArticleCategoryResponseDto): void {
    if (this.isEdit()) return;
    this.previousMode = this.viewMode();
    this.selectedCategory = category;
    this.setViewMode('edit');
    this.categoryForm.patchValue({
      name: category.name,
      tva:  category.tva
    });
    this.cdr.markForCheck();
  }

  openView(category: ArticleCategoryResponseDto): void {
    if (this.isView()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('view');
    this.selectedCategory = category;
    this.cdr.markForCheck();
  }

  cancel(): void {
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

  restore(cat: ArticleCategoryResponseDto): void {
    this.categoryService.restore(cat.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('articles.categories.responses.success.category_restored', { name: cat.name }));
        if (this.isView()) this.cancel();
        this.reload();
      },
      error: (err: HttpErrorResponse) => {
        const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
        this.flash('error', msg);
      },
    });
  }

  submit(): void {
    if (this.categoryForm.invalid) return;
    const dto: CategoryRequestDto = this.categoryForm.value;

    if (this.isCreate()) {
      this.categoryService.create(dto).subscribe({
        next: () => {
          this.cancel();
          this.reload();
          this.flash('success', this.translate.instant('articles.categories.responses.success.category_created', { name: dto.name }));
        },
        error: (err: HttpErrorResponse) => {
          console.log(err);
          const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
          this.flash('error', msg);
        },
      });
    } else if (this.isEdit() && this.selectedCategory) {
      this.categoryService.update(this.selectedCategory.id, dto).subscribe({
        next: (updated) => {
          this.cancel();
          this.selectedCategory= updated;
          this.flash('success', this.translate.instant('articles.categories.responses.success.category_updated'));        },
        error: (err: HttpErrorResponse) => {
          const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
          this.flash('error', msg);
        },
      });
    }
  }

  delete(category: ArticleCategoryResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant('articles.categories.confirmations.delete_category.title'),
        message:     this.translate.instant('articles.categories.confirmations.delete_category.message', { name: category.name }),
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
        this.categoryService.delete(category.id).subscribe({
          next: () => {
            if (this.isView()) this.cancel();
            this.flash('success', this.translate.instant('articles.categories.responses.success.category_deleted', { name: category.name }));
            this.reload();
          },
        error: (err: HttpErrorResponse) => {
          const msg = err.error?.message || this.translate.instant('articles.responses.errors.SERVER_ERROR');
          this.flash('error', msg);
        },
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

  trackById(_: number, c: ArticleCategoryResponseDto): string { return c.id; }

  setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
    this.cdr.markForCheck();
  }
}