import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';

import { ArticleService, ArticleResponseDto, ArticleStatsDto, CreateArticleRequestDto, UpdateArticleRequestDto, UnitEnum } from './../../../services/articles/articles.service';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { CurrencyConfigService } from '../../../services/currency-config.service';
import { ModalComponent } from '../../modal/modal';
import { PaginationComponent } from '../../pagination/pagination';
import { HttpError } from '../../../interfaces/HttpError';
import { ArticleCategoryResponseDto, CategoryService } from '../../../services/articles/categories.service';
import { MatTableDataSource } from '@angular/material/table';
import { TranslatePipe } from '@ngx-translate/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RegexPatterns } from '../../../interfaces/RegexPatterns';

type ViewMode = 'list' | 'list-deleted' | 'create' | 'edit' | 'view';

@Component({
  selector: 'app-article',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIcon, RouterLink, RouterLinkActive, PaginationComponent, TranslatePipe, MatTooltipModule],
  templateUrl: './home.html',
  styleUrls: ['./home.scss'],
})
export class ArticleComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);
  private readonly location= inject(Location)

  dataSource = new MatTableDataSource<ArticleResponseDto>([]);

  categories: ArticleCategoryResponseDto[] = [];
  stats: ArticleStatsDto | null = null;

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  selectedArticle: ArticleResponseDto | null = null;
  articleIdFromRoute: string | null =null;
  loading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  searchQuery = '';

  readonly PRIVILEGES = PRIVILEGES;
  articleForm: FormGroup;

  sortColumn: string = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  viewMode = signal<ViewMode>('list');
  isMode = (mode: ViewMode) => computed(() => this.viewMode() === mode);

  isList = this.isMode('list');
  isDeletedList = this.isMode('list-deleted');
  isCreate = this.isMode('create');
  isEdit = this.isMode('edit');
  isView = this.isMode('view');
  private previousMode: ViewMode = 'list';

  readonly units = Object.keys(UnitEnum)
    .filter(key => isNaN(Number(key)))  // Only keep the enum names, not the reverse mapping numbers
    .map(key => ({ value: key, label: key }));

  constructor(
    public authService: AuthService,
    private articleService: ArticleService,
    private categoryService: CategoryService,
    private currencyConfig: CurrencyConfigService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    private route: ActivatedRoute
  ) {
    this.articleForm = this.fb.group({
      libelle:    ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200), Validators.pattern(RegexPatterns.alphaNumeric)]],
      prix:       [null, [Validators.required, Validators.min(0.01)]],
      categoryId: ['', Validators.required],
      unit:       ['', Validators.required],
      barCode:    ['', [Validators.required, Validators.minLength(8), Validators.maxLength(13), Validators.pattern(RegexPatterns.barCode)]],
      tva:        [null, [Validators.min(0.01), Validators.max(100), Validators.pattern(RegexPatterns.integer)]],
    });
  }

  ngOnInit(): void {
    this.dataSource.filterPredicate = (data, filter) => {
      return this.flattenObject(data).includes(filter);
    };

    this.articleIdFromRoute = this.route.snapshot.paramMap.get('id');
    if(this.articleIdFromRoute == null){
      this.reload();
      return;
    }

    this.articleService.getById(this.articleIdFromRoute).subscribe({
      next: (res)=>{
        this.selectedArticle= res;
        this.setViewMode('view');
        this.reload();
      },
      error: (error)=>{
        const err= error.error as HttpError;
        this.flash("error", err.message);
        this.cancel();
      }
    });

    this.reload();
  }

  // ── Page title ────────────────────────────────────────────────────────────

  get pageTitle(): string {
    if (this.isCreate())      return 'articles.title_add';
    if (this.isEdit())        return 'articles.title_edit';
    if (this.isView())        return 'articles.title_details';
    if (this.isDeletedList()) return 'articles.title_deleted';
    return 'articles.title_list';
  }

  // ── Stats ─────────────────────────────────────────────────────────────────

  get activeCount():    number { return this.stats?.activeCount    ?? 0; }
  get deletedCount():   number { return this.stats?.deletedCount   ?? 0; }
  get totalCountStat(): number { return this.stats?.totalCount     ?? 0; }
  get categoriesCount():number { return this.stats?.categoriesCount ?? 0; }

  // ── Currency ──────────────────────────────────────────────────────────────

  get currencyCode():   string { return this.currencyConfig.code;   }
  get currencyLocale(): string { return this.currencyConfig.locale; }

  // ── Search ────────────────────────────────────────────────────────────────

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

  // ── Load ──────────────────────────────────────────────────────────────────

  load(): void {
    this.errors = [];
    this.articleService.getAll(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items;
        this.totalCount = res.totalCount;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.flash('error', this.translate.instant('errors.internal_error'));
        this.loading = false;
      },
    });
  }

  loadDeleted(): void {
    this.articleService.getDeleted(this.pageNumber(), this.pageSize()).subscribe({
      next: (res) => {
        this.dataSource.data = res.items;
        this.totalCount = res.totalCount;
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.internal_error')),
    });
  }

  loadCategories(): void {
    this.categoryService.getAll().subscribe({
      next: (cats) => { this.categories = cats; this.cdr.markForCheck(); },
      error: () => this.flash('error', this.translate.instant('errors.internal_error')),
    });
  }

  loadStats(): void {
    this.articleService.getStats().subscribe({
      next: (res) => {
        this.stats = res;
        if (this.isDeletedList() && res.deletedCount === 0) {
          this.setViewMode('list');
          this.load();
        }
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('errors.internal_error')),
    });
  }

  reload(): void {
    if (this.isDeletedList()) {
      this.loadDeleted();
    } else {
      this.load();
    }
    this.loadCategories();
    this.loadStats();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  onDeletedCardClick(): void {
    if (this.isDeletedList() || this.deletedCount < 1) return;
    this.setViewMode('list-deleted');
    this.loadDeleted();
  }

  onActiveCardClick(): void {
    if (this.isList()) return;
    this.setViewMode('list');
    this.load();
  }

  openCreate(): void {
    if (this.isCreate()) return;
    this.setViewMode('create');
    this.selectedArticle = null;
    this.articleForm.reset({ libelle: '', prix: null, unit: null, categoryId: '', barCode: '', tva: null, });
  }

  openEdit(article: ArticleResponseDto): void {
    if (this.isEdit()) return;
    this.previousMode = this.viewMode();
    this.selectedArticle = article;
    this.setViewMode('edit');
    this.articleForm.patchValue({
      libelle:    article.libelle,
      prix:       article.prix,
      unit:       article.unit,
      categoryId: article.category.id,
      barCode:    article.barCode,
      tva:        article.tva,
    });
    this.cdr.markForCheck();
  }

  openView(article: ArticleResponseDto): void {
    if (this.isView()) return;
    this.previousMode = this.viewMode();
    this.setViewMode('view');
    this.selectedArticle = article;
    this.cdr.markForCheck();
  }

  cancel(): void {
    if(this.articleIdFromRoute) this.location.back();
    const target = this.resolveCancel();
    const needsCategory: ViewMode[] = ['view', 'edit'];

    this.setViewMode(target);

    if (!needsCategory.includes(target)) {
      this.selectedArticle = null;
    }

    if (target !== 'edit') {
      this.articleForm.reset();
    }
  }

  private resolveCancel(): ViewMode {
    const current = this.viewMode();

    // edit → view: only go back to view if selectedCategory is still available
    if (current === 'edit' && this.previousMode === 'view' && this.selectedArticle) {
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

  submit(): void {
    if (this.articleForm.invalid) return;
    const val = this.articleForm.value;

    if (this.isCreate()) {
      const dto: CreateArticleRequestDto = {
        libelle:    val.libelle,
        prix:       val.prix,
        unit:       val.unit,
        categoryId: val.categoryId,
        barCode:    val.barCode,
        tva: val.tva};
      this.articleService.create(dto).subscribe({
        next: () => {
          this.reload();
          this.cancel();
          this.flash('success', this.translate.instant('articles.responses.success.article_created', { name: val.libelle }));
        },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('errors.internal_error')),
      });

    } else if (this.isEdit() && this.selectedArticle) {
      const dto: UpdateArticleRequestDto = {
        libelle:    val.libelle,
        prix:       val.prix,
        unit:       val.unit,
        categoryId: val.categoryId,
        barCode:    val.barCode ?? undefined,
        tva: val.tva,
      };
      this.articleService.update(this.selectedArticle.id, dto).subscribe({
        next: () => {
          this.cancel();
          this.reload();
          this.flash('success', this.translate.instant('articles.responses.success.article_updated', { name: val.libelle }));
        },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('errors.internal_error')),
      });
    }
  }

  delete(article: ArticleResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant('articles.confirmations.delete_article.title'),
        message:     this.translate.instant('articles.confirmations.delete_article.message', { name: article.libelle }),
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
        this.articleService.delete(article.id).subscribe({
          next: () => {
            if (this.isView()) this.cancel();
            this.flash('success', this.translate.instant('articles.responses.success.article_deleted', { name: article.libelle }));
            this.reload();
          },
          error: () => this.flash('error', this.translate.instant('errors.internal_error')),
        });
      });
  }

  restore(article: ArticleResponseDto): void {
    this.articleService.restore(article.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('articles.responses.success.article_restored', { name: article.libelle }));
        this.reload();
        if (this.isView()) this.cancel();
      },
      error: (error) => {
        const err = error.error as HttpError;
        this.flash('error', err?.message ?? this.translate.instant('errors.internal_error'));
      }
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

  trackById(_: number, a: ArticleResponseDto): string { return a.id; }
  trackByIndex(index: number, item: any){
    return index;
  }

  private flattenObject(obj: any): string {
    return Object.keys(obj)
      .map(key => {
        const value = obj[key];
        if (value && typeof value === 'object') {
          return this.flattenObject(value);
        }
        return value;
      })
      .join(' ')
      .toLowerCase();
  }

  private getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((acc, key) => acc?.[key], obj);
  }

  setViewMode(mode: ViewMode) {
    this.viewMode.set(mode);
    this.cdr.markForCheck();
  }
}