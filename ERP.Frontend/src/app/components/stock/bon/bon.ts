import { BonEntreResponse, BonSortieResponse } from './../../../services/stock.service';
import {
  Component, OnInit, ChangeDetectorRef, ViewEncapsulation,
  DestroyRef, inject, signal, computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PRIVILEGES, AuthService } from '../../../services/auth/auth.service';
import {
  StockService, CreateBonEntreRequest, UpdateBonEntreRequest, CreateBonSortieRequest, UpdateBonSortieRequest,
  BonRetourResponse, CreateBonRetourRequest, UpdateBonRetourRequest,
  LigneResponseDto, LigneRequestDto, RetourSourceType, BonStatsDto,
  PagedResult,
} from '../../../services/stock.service';
import { UnitEnum } from '../../../services/articles/articles.service';
export type BonRecord = BonEntreResponse | BonSortieResponse | BonRetourResponse;
import { MatTableDataSource } from '@angular/material/table';
import { HttpError } from '../../../interfaces/HttpError';
import { MatDialog } from '@angular/material/dialog';
import { ModalComponent } from '../../modal/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PaginationComponent } from '../../pagination/pagination';
import { Observable, switchMap, EMPTY, forkJoin, map, catchError, of, Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { RouterLink } from "@angular/router";
import { ClientResponseDto, ClientsService } from '../../../services/clients/clients.service';
import { ArticleResponseDto } from '../../../services/articles/articles.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { FournisseurResponse } from '../../../services/fournisseur.service';
import { CurrencyConfigService } from '../../../services/currency-config.service';
import { take } from 'rxjs';

export type BonType = 'entre' | 'sortie' | 'retour';

type BonApi = {
  list:         (p: number, s: number) => Observable<PagedResult<BonRecord>>;
  delete:       (id: string) => Observable<any>;
};

type ViewMode = 'list' | 'create' | 'edit' | 'view';

export interface PendingLigne {
  _localId:  string;
  articleId: string;
  articleLabel: string;
  quantity:  number;
  price:     number;
  remarque:  string | null;
  total:     number;
}

@Component({
  selector: 'app-bons',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    MatIconModule, MatButtonModule, MatTooltipModule,
    PaginationComponent,
    RouterLink,
    TranslatePipe
  ],
  templateUrl: './bon.html',
  styleUrl: './bon.scss',
  encapsulation: ViewEncapsulation.None,
})
export class BonsComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);
  readonly PRIVILEGES   = PRIVILEGES;
  readonly RetourSource = RetourSourceType;

  activeBonType: BonType = 'entre';

  dataSource = new MatTableDataSource<BonRecord>([]);
  pageNumber = signal(1);
  pageSize   = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  sortColumn    = '';
  sortDirection: 'asc' | 'desc' = 'asc';
  searchQuery   = '';

  private masterArticles: ArticleResponseDto[] = [];
  articles: ArticleResponseDto[] = [];
  private masterStockMap = new Map<string, number>();
  private masterRetourMap = new Map<string, number>();

  viewMode = signal<ViewMode>('list');
  isList   = computed(() => this.viewMode() === 'list');
  isCreate = computed(() => this.viewMode() === 'create');
  isEdit   = computed(() => this.viewMode() === 'edit');
  isView   = computed(() => this.viewMode() === 'view');
  private previousMode: ViewMode = 'list';

  errors: string[] = [];
  successMessage: string | null = null;

  selectedBon: BonRecord | null = null;

  allSourceBons: { id: string; numero: string; sourceType: RetourSourceType }[] = [];

  dateFrom: string | null = null;
  dateTo: string | null = null;

  headerForm: FormGroup;
  ligneForm:  FormGroup;

  pendingLignes: PendingLigne[] = [];
  inlineLigneLocalId: string | null = null;
  inlineLigneOpen = false;
  isInlineLigneSubmitting = false;

  clients: ClientResponseDto[] = [];
  clientPage = 1;
  clientPageSize = 10;
  clientTotalCount = 0;
  clientSearchQuery = '';
  clientsLoading = false;
  hasMoreClients = true;
  clientDropdownOpen = false;
  selectedClientLabel = '';
  private clientSearchSubject$ = new Subject<string>();

  fournisseurDropdownOpen = false;
  fournisseurs: FournisseurResponse[] = [];
  fournisseurPage = 1;
  fournisseurPageSize = 10;
  fournisseurTotalCount = 0;
  fournisseurSearchQuery = '';
  fournisseursLoading = false;
  hasMoreFournisseurs = true;
  selectedFournisseurLabel = '';
  private fournisseurSearchSubject$ = new Subject<string>();

  articleDropdownOpen = false;
  articleDropdownItems: ArticleResponseDto[] = [];
  articlePage = 1;
  articlePageSize = 10;
  articleTotalCount = 0;
  articleSearchQuery = '';
  articlesLoading = false;
  hasMoreArticles = true;
  selectedArticleLabel = '';
  private articleSearchSubject$ = new Subject<string>();

  private fournisseurCache = new Map<string, string>();
  fournisseurNames = signal<Map<string, string>>(new Map());

  constructor(
    private stock: StockService,
    public authService: AuthService,
    public clientService: ClientsService,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef,
    private dialog: MatDialog,
    private currencyConfig: CurrencyConfigService
  ) {
    this.headerForm = this.buildHeaderForm();
    this.ligneForm  = this.buildLigneForm();
  }

  ngOnInit(): void {
    this.dataSource.filterPredicate = (data, filter) =>
      this.flattenObject(data).includes(filter);
    this.reload();
    this.initClientSearch();
    this.initFournisseurSearch();
    this.initArticleSearch();
  }

  private initClientSearch(): void {
    this.clientSearchSubject$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(query => {
        this.clientSearchQuery = query;
        this.clientPage = 1;
        this.hasMoreClients = true;
        this.loadClients(1, false);
      });
  }

  private buildHeaderForm(): FormGroup {
    const form = this.fb.group({
      observation:   ['', Validators.maxLength(500)],
      fournisseurId: [''],
      clientId:      [''],
      sourceId:      [''],
      sourceType:    [RetourSourceType.BonEntre],
      motif:         ['', Validators.maxLength(500)],
    });

    // Apply conditional validators based on bon type
    this.applyHeaderValidators(form);
    return form;
  }

  private applyHeaderValidators(form: FormGroup): void {
    const { fournisseurId, clientId, sourceId, motif } = form.controls;

    fournisseurId.clearValidators();
    clientId.clearValidators();
    sourceId.clearValidators();
    motif.clearValidators();

    if (this.activeBonType === 'entre') {
      fournisseurId.setValidators(Validators.required);
    } else if (this.activeBonType === 'sortie') {
      clientId.setValidators(Validators.required);
    } else if (this.activeBonType === 'retour') {
      sourceId.setValidators(Validators.required);
      motif.setValidators([Validators.required, Validators.maxLength(500)]);
    }

    Object.values(form.controls).forEach(c => c.updateValueAndValidity());
  }

  private buildLigneForm(): FormGroup {
    return this.fb.group({
      articleId: ['', Validators.required],
      quantity:  [1,  [Validators.required, Validators.min(1)]],
      price:     [0,  [Validators.required, Validators.min(0)]],
      remarque:  [''],
    });
  }

  private applyTypeValidators(): void {
    const f = this.headerForm;
    ['fournisseurId', 'clientId', 'sourceId', 'motif'].forEach(ctrl =>
      f.get(ctrl)?.clearValidators()
    );
    if (this.activeBonType === 'entre') {
      f.get('fournisseurId')?.setValidators(Validators.required);
    } else if (this.activeBonType === 'sortie') {
      f.get('clientId')?.setValidators(Validators.required);
    } else {
      f.get('sourceId')?.setValidators(Validators.required);
      f.get('motif')?.setValidators(Validators.required);
    }
    ['fournisseurId', 'clientId', 'sourceId', 'motif'].forEach(ctrl =>
      f.get(ctrl)?.updateValueAndValidity()
    );
  }

  sortBy(column: string): void {
    this.sortDirection = this.sortColumn === column && this.sortDirection === 'asc' ? 'desc' : 'asc';
    this.sortColumn = column;
  }

  get sortedData(): BonRecord[] {
    const data = [...this.dataSource.filteredData];
    if (!this.sortColumn) return data;
    return data.sort((a, b) => {
      let va = this.getNestedValue(a, this.sortColumn);
      let vb = this.getNestedValue(b, this.sortColumn);
      if (va == null) return 1;
      if (vb == null) return -1;
      if (typeof va === 'string') va = va.toLowerCase();
      if (typeof vb === 'string') vb = vb.toLowerCase();
      return (va < vb ? -1 : va > vb ? 1 : 0) * (this.sortDirection === 'asc' ? 1 : -1);
    });
  }

  applyFilter(): void {
    this.dataSource.filter = this.searchQuery.trim().toLowerCase();
  }

  applyDateFilter(): void {
    if (!this.dateFrom || !this.dateTo) return;
    if (this.dateFrom > this.dateTo) {
      [this.dateFrom, this.dateTo] = [this.dateTo, this.dateFrom];
    }
    const from = new Date(this.dateFrom);
    const to = new Date(this.dateTo);
    let request$: Observable<PagedResult<BonRecord>>;
    switch (this.activeBonType) {
      case 'entre':
        request$ = this.stock.getBonEntresByDateRange(from, to, this.pageNumber(), this.pageSize());
        break;
      case 'sortie':
        request$ = this.stock.getBonSortiesByDateRange(from, to, this.pageNumber(), this.pageSize());
        break;
      case 'retour':
        request$ = this.stock.getBonRetoursByDateRange(from, to, this.pageNumber(), this.pageSize());
        break;
    }
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.dataSource.data = res.items;
        this.totalCount = res.totalCount;
        this.pageNumber.set(1);
        this.cdr.markForCheck();
      },
      error: () => this.flash('error', this.translate.instant('STOCK.BONS.ERRORS.DATE_FILTER_FAILED'))
    });
  }

  clearDateFilter(): void {
    this.dateFrom = '';
    this.dateTo = '';
    this.pageNumber.set(1);
    this.reload();
  }

  switchBonType(type: BonType): void {
    this.activeBonType = type;
    this.pageNumber.set(1);
    this.dateFrom = '';
    this.dateTo = '';
    this.searchQuery = '';
    this.setViewMode('list');
    this.masterRetourMap.clear();
    this.reload();
  }

  onPageChange(page: number): void {
    this.pageNumber.set(page);
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageNumber.set(1);
    this.reload();
  }


  get currencyCode():   string { return this.currencyConfig.code;   }
  get currencyLocale(): string { return this.currencyConfig.locale; }

  reload(): void {
    const list$ = this.bonApi[this.activeBonType].list(this.pageNumber(), this.pageSize());

    const articles$ = this.activeBonType === 'sortie'
      ? forkJoin({
          arts: this.stock.getArticlesPaged(1, 20),
          stock: this.stock.getStockArticles(),
        })
      : this.stock.getArticlesPaged(1, 20).pipe(map(arts => ({ arts, stock: null })));

    let fournisseurs$: Observable<{ items: FournisseurResponse[]; totalCount: number } | null> = of(null);
    let sourceBons$: Observable<any> = of(null);

    if (this.activeBonType === 'entre') {
      fournisseurs$ = this.stock.getFournisseursPaged(1, 1000).pipe(
        map(res => ({ items: res.items, totalCount: res.totalCount }))
      );
    } else if (this.activeBonType === 'retour') {
      sourceBons$ = forkJoin({
        entres: this.stock.getBonEntres(1, 1000),
        sorties: this.stock.getBonSorties(1, 1000),
      });
    }

    forkJoin({ list: list$, articles: articles$, fournisseurs: fournisseurs$, sourceBons: sourceBons$ })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ list, articles, fournisseurs, sourceBons }) => {
          this.dataSource.data = list.items;
          this.totalCount = list.totalCount;

          if (this.activeBonType === 'entre') {
            const ids = list.items
              .map(b => (b as BonEntreResponse).fournisseurId)
              .filter((id): id is string => !!id);
            if (ids.length) this.prefetchFournisseurs(ids);
          }

          this.masterArticles = articles.arts.items.filter(a => !a.isDeleted);
          if (articles.stock) {
            this.masterStockMap.clear();
            articles.stock.inStock.forEach((s: any) => {
              this.masterStockMap.set(s.articleId ?? s.id, s.quantity);
            });
          }

          if (this.activeBonType === 'entre' && fournisseurs && fournisseurs.items) {
            this.fournisseurs = fournisseurs.items.filter(f => !f.isDeleted && !f.isBlocked);
            if (this.isList() && this.fournisseurs.length > 0) {
              this.headerForm.patchValue({ fournisseurId: this.fournisseurs[0].id }, { emitEvent: false });
            }
          }

          if (sourceBons) {
            this.allSourceBons = [
              ...sourceBons.entres.items.map((b: BonEntreResponse) => ({
                id: b.id,
                numero: b.numero,
                sourceType: RetourSourceType.BonEntre,
              })),
              ...sourceBons.sorties.items.map((b: BonSortieResponse) => ({
                id: b.id,
                numero: b.numero,
                sourceType: RetourSourceType.BonSortie,
              })),
            ].sort((a, b) => a.numero.localeCompare(b.numero));

            if (this.isList() && this.allSourceBons.length > 0) {
              this.headerForm.patchValue(
                { sourceId: this.allSourceBons[0].id, sourceType: this.allSourceBons[0].sourceType },
                { emitEvent: false }
              );
            }
          }

          this.syncArticles();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.flash(
            'error',
            (err.error as HttpError)?.message ?? this.translate.instant('STOCK.BONS.ERRORS.LOAD_FAILED')
          );
        },
      });
  }

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }

  get activeCount(): number { return this.sortedData.length ?? 0; }

  get pendingTotal(): number {
    return this.pendingLignes.reduce((s, l) => s + l.total, 0);
  }

  loadSourceBons(): void {
    forkJoin({
      entres:  this.stock.getBonEntres(1, 1000),
      sorties: this.stock.getBonSorties(1, 1000),
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ entres, sorties }) => {
          this.allSourceBons = [
            ...entres.items.map(b => ({
              id: b.id, numero: b.numero, sourceType: RetourSourceType.BonEntre,
            })),
            ...sorties.items.map(b => ({
              id: b.id, numero: b.numero, sourceType: RetourSourceType.BonSortie,
            })),
          ].sort((a, b) => a.numero.localeCompare(b.numero));
          if (this.allSourceBons.length > 0) {
            this.headerForm.patchValue({ sourceId: this.allSourceBons[0].id, sourceType: this.allSourceBons[0].sourceType });
          }
          this.cdr.markForCheck();
        },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('STOCK.BONS.ERRORS.LOAD_SOURCE_BONS_FAILED')),
      });
  }

  loadArticles(): void {
    const arts$ = this.stock.getArticlesPaged(1, 1000);
    const stock$ = this.activeBonType === 'sortie'
      ? this.stock.getStockArticles()
      : of(null);

    forkJoin({ arts: arts$, stock: stock$ })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ arts, stock }) => {
          this.masterStockMap.clear();

          if (stock) {
            stock.inStock.forEach((s: any) => {
              this.masterStockMap.set(s.articleId ?? s.id, s.quantity);
            });

            this.masterArticles = arts.items.filter(
              a => !a.isDeleted &&
                  this.masterStockMap.has(a.id) &&
                  (this.masterStockMap.get(a.id) ?? 0) > 0
            );
          } else {
            this.masterArticles = arts.items.filter(a => !a.isDeleted);
            this.articleDropdownItems = this.masterArticles;
          }

          this.syncArticles();
        },
        error: () => this.flash('error', this.translate.instant('STOCK.BONS.ERRORS.LOAD_ARTICLES_FAILED'))
      });
  }

  private updateQuantityValidator(max: number | null): void {
    const ctrl = this.ligneForm.get('quantity')!;
    const validators = [Validators.required, Validators.min(0.001)];
    if (max !== null) validators.push(Validators.max(max));
    ctrl.setValidators(validators);
    ctrl.updateValueAndValidity();
  }

  loadClients(page: number, append = false): void {
    if (this.clientsLoading) return;
    this.clientsLoading = true;

    this.stock.getClientsPaged(page, this.clientPageSize, this.clientSearchQuery)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.clients = append
            ? [...this.clients, ...res.items]
            : res.items;
          this.clientTotalCount = res.totalCount;
          this.clientPage = page;
          this.hasMoreClients = this.clients.length < res.totalCount;
          this.clientsLoading = false;
          if (!append && this.clients.length > 0 && !this.headerForm.get('clientId')?.value) {
            this.selectClient(this.clients[0]);
          }
          if (this.isEdit()) this.restoreClientLabel();
          this.cdr.markForCheck();
        },
        error: () => {
          this.clientsLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  onSourceBonChange(id: string): void {
    const match = this.allSourceBons.find(b => b.id === id);
    if (!match) return;

    this.headerForm.patchValue({ sourceType: match.sourceType });

    if (this.isCreate() && this.activeBonType === 'retour') {
      const $req: Observable<BonRecord> = match.sourceType === RetourSourceType.BonEntre
        ? this.stock.getBonEntreById(id)
        : this.stock.getBonSortieById(id);

      $req.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res) => {
          const aggregated = new Map<string, PendingLigne>();
          for (const l of res.lignes) {
            const existing = aggregated.get(l.articleId);
            if (existing) {
              existing.quantity += l.quantity;
              existing.total = existing.quantity * existing.price;
            } else {
              aggregated.set(l.articleId, {
                _localId: crypto.randomUUID(),
                articleId: l.articleId,
                articleLabel: this.getArticleLabel(l.articleId),
                quantity: l.quantity,
                price: l.price,
                remarque: l.remarque,
                total: l.quantity * l.price,
              });
            }
          }
          this.pendingLignes = Array.from(aggregated.values());
          this.masterRetourMap.clear();
          for (const [id, ligne] of aggregated) {
            this.masterRetourMap.set(id, ligne.quantity);
          }
          this.syncArticles();
          this.cdr.markForCheck();
        },
        error: (err) => {
          const error = err.error as HttpError;
          this.flash('error', error.message ?? this.translate.instant('STOCK.BONS.ERRORS.LOAD_SOURCE_BON_LIGNES_FAILED'));
        }
      });
    }
  }

  private getArticleLabel(articleId: string): string {
    const article = this.articles.find(a => a.id === articleId);
    return article ? `${article.codeRef} — ${article.libelle}` : articleId;
  }

  openCreate(): void {
    if (this.isCreate()) return;
    this.masterRetourMap.clear();
    this.syncArticles();

    this.previousMode = this.viewMode();
    this.headerForm.reset({ numero: '', observation: '', sourceType: RetourSourceType.BonEntre });
    this.pendingLignes = [];
    this.inlineLigneOpen = false;
    this.inlineLigneLocalId = null;
    this.applyTypeValidators();
    if (this.activeBonType === 'retour') this.loadSourceBons();
    if (this.activeBonType === 'sortie') this.loadClients(1, false);
    if (this.activeBonType === 'entre')  this.loadFournisseurs(1, false);
    this.loadArticles();
    this.setViewMode('create');
  }

  openView(bon: BonRecord): void {
    if (this.isView()) return;
    this.syncArticles();
    this.previousMode = this.viewMode();
    const pristine = this.dataSource.data.find(b => b.id === bon.id) ?? bon;
    this.selectedBon = { ...bon, lignes: bon.lignes.map(l => ({ ...l })) };
    this.setViewMode('view');
  }

  openEdit(bon: BonRecord): void {
    if (this.isEdit()) return;
    this.masterRetourMap.clear();
    this.syncArticles();
    this.previousMode = this.viewMode();
    const pristine = this.dataSource.data.find(b => b.id === bon.id) ?? bon;
    this.selectedBon = {
      ...pristine,
      lignes: pristine.lignes.map(l => ({ ...l })),
    };
    this.pendingLignes = [];
    this.inlineLigneOpen = false;
    this.inlineLigneLocalId = null;
    this.applyTypeValidators();
    if (this.activeBonType === 'retour') this.loadSourceBons();
    if (this.activeBonType === 'entre')  this.loadFournisseurs(1, false);
    if (this.activeBonType === 'sortie') this.loadClients(1, false);
    this.loadArticles();
    this.headerForm.patchValue({
      observation:   pristine.observation                           ?? '',
      fournisseurId: (pristine as BonEntreResponse).fournisseurId  ?? '',
      clientId:      (pristine as BonSortieResponse).clientId      ?? '',
      sourceId:      (pristine as BonRetourResponse).sourceId      ?? '',
      sourceType:    (pristine as BonRetourResponse).sourceType    ?? RetourSourceType.BonEntre,
      motif:         (pristine as BonRetourResponse).motif         ?? '',
    });
    this.setViewMode('edit');
  }

  cancel(): void {
    this.inlineLigneOpen = false;
    this.inlineLigneLocalId = null;
    this.pendingLignes = [];

    const target = this.resolveCancel();
    this.setViewMode(target);

    if (target === 'view' && this.selectedBon) {
      const pristine = this.dataSource.data.find(b => b.id === this.selectedBon!.id);
      if (pristine) {
        this.selectedBon = { ...pristine, lignes: pristine.lignes.map(l => ({ ...l })) };
      }
    } else if (!['view', 'edit'].includes(target)) {
      this.selectedBon = null;
    }

    if (target !== 'edit') {
      this.headerForm.reset();
    }
  }

  private resolveCancel(): ViewMode {
    const cur = this.viewMode();
    if (cur === 'edit' && this.previousMode === 'view' && this.selectedBon) return 'view';
    if (cur === 'view' && (this.previousMode === 'list')) return this.previousMode;
    if (cur === 'create') return this.previousMode ?? 'list';
    return 'list';
  }

  openInlineLigneAdd(): void {
    this.inlineLigneLocalId = null;
    this.ligneForm = this.buildLigneForm();
    if (this.articles.length > 0) {
      const first = this.articles[0];
      this.ligneForm.patchValue({ articleId: first.id, price: first.prix });
    }
    this.inlineLigneOpen = true;
    this.cdr.markForCheck();
  }

  openInlineLigneEdit(ligne: PendingLigne | LigneRequestDto, isLocal: boolean): void {
    this.ligneForm = this.buildLigneForm();
    if (isLocal) {
      const pl = ligne as PendingLigne;
      this.inlineLigneLocalId = pl._localId;
      this.syncArticles();
      this.ligneForm.patchValue({ articleId: pl.articleId, quantity: pl.quantity, price: pl.price, remarque: pl.remarque ?? '' });
    } else {
      const sl = ligne as LigneResponseDto;
      this.inlineLigneLocalId = sl.id;
      this.syncArticles();
      this.ligneForm.patchValue({ articleId: sl.articleId, quantity: sl.quantity, price: sl.price, remarque: (sl as any).remarque ?? '' });
    }
    const max = this.getArticleMaxQty(this.ligneForm.get('articleId')!.value);
    this.updateQuantityValidator(max === Infinity ? null : max);
    this.inlineLigneOpen = true;
    this.cdr.markForCheck();
  }

  closeInlineLigne(): void {
    this.inlineLigneOpen = false;
    this.inlineLigneLocalId = null;
    this.ligneForm = this.buildLigneForm();
    this.syncArticles();
  }

  submitInlineLigne(): void {
    if (this.ligneForm.invalid || this.isInlineLigneSubmitting) return;
    const val = this.ligneForm.value;

    const master = this.masterArticles.find(a => a.id === val.articleId);
    if (!master) {
      this.flash('error', this.translate.instant('STOCK.BONS.ERRORS.ARTICLE_NOT_FOUND'));
      return;
    }

    const label = `${master.codeRef} — ${master.libelle}`;
    const activeLignes: any[] = this.isCreate()
      ? this.pendingLignes
      : (this.selectedBon?.lignes ?? []);
    const alreadyConsumed = activeLignes
      .filter(l => {
        const lid = (l as any)._localId ?? (l as any).id;
        return l.articleId === val.articleId && lid !== this.inlineLigneLocalId;
      })
      .reduce((sum, l) => sum + l.quantity, 0);
    const max = this.getArticleMaxQty(val.articleId);

    if (max !== Infinity) {
      if (val.quantity > max) {
        this.flash('error', this.translate.instant('STOCK.ERRORS.INSUFFICIENT_STOCK', {
          max, requested: val.quantity
        }));
        return;
      }
      if (!this.inlineLigneLocalId && alreadyConsumed > 0) {
        const combined = alreadyConsumed + val.quantity;
        const masterMax = this.activeBonType === 'sortie'
          ? (this.masterStockMap.get(val.articleId) ?? 0)
          : (this.masterRetourMap.get(val.articleId) ?? 0);
        if (combined > masterMax) {
          this.flash('error', this.translate.instant('STOCK.ERRORS.MERGED_QUANTITY_EXCEEDS_STOCK', {
            article: master.libelle, total: combined, max: masterMax
          }));
          return;
        }
      }
    }

    if (this.isCreate()) {
      if (this.inlineLigneLocalId) {
        const idx = this.pendingLignes.findIndex(l => l._localId === this.inlineLigneLocalId);
        if (idx !== -1) {
          this.pendingLignes[idx] = {
            ...this.pendingLignes[idx],
            articleId: val.articleId, articleLabel: label,
            quantity: val.quantity, price: val.price,
            remarque: val.remarque || null,
            total: val.quantity * val.price,
          };
        }
      } else {
        const existingIndex = this.pendingLignes.findIndex(l => l.articleId === val.articleId);
        if (existingIndex !== -1) {
          const existing = this.pendingLignes[existingIndex];
          const newQty = existing.quantity + val.quantity;
          this.pendingLignes[existingIndex] = {
            ...existing, quantity: newQty, total: newQty * existing.price,
          };
        } else {
          this.pendingLignes.push({
            _localId: crypto.randomUUID(),
            articleId: val.articleId, articleLabel: label,
            quantity: val.quantity, price: val.price,
            remarque: val.remarque || null,
            total: val.quantity * val.price,
          });
        }
      }
      this.closeInlineLigne();
      this.syncArticles();
      this.cdr.markForCheck();
      return;
    }

    if (this.isEdit() && this.selectedBon) {
      if (this.inlineLigneLocalId) {
        const idx = this.selectedBon.lignes.findIndex(l => l.id === this.inlineLigneLocalId);
        if (idx !== -1) {
          this.selectedBon.lignes[idx] = {
            ...this.selectedBon.lignes[idx],
            articleId: val.articleId, quantity: val.quantity,
            price: val.price, remarque: val.remarque || null,
            total: val.quantity * val.price,
          };
        }
      } else {
        const existingIndex = this.selectedBon.lignes.findIndex(l => l.articleId === val.articleId);
        if (existingIndex !== -1) {
          const existing = this.selectedBon.lignes[existingIndex];
          const newQty = existing.quantity + val.quantity;
          this.selectedBon.lignes[existingIndex] = {
            ...existing, quantity: newQty, total: newQty * existing.price,
          };
        } else {
          this.selectedBon.lignes.push({
            id: `temp_${crypto.randomUUID()}`,
            articleId: val.articleId, quantity: val.quantity,
            price: val.price, remarque: val.remarque || null,
            total: val.quantity * val.price,
          } as LigneResponseDto);
        }
      }
      this.closeInlineLigne();
      this.syncArticles();
      this.cdr.markForCheck();
      return;
    }
  }

  removePendingLigne(localId: string): void {
    this.pendingLignes = this.pendingLignes.filter(l => l._localId !== localId);
    this.cdr.markForCheck();
  }

  submit(): void {
    if (this.headerForm.invalid) return;

    if (this.isCreate() && this.pendingLignes.length === 0) {
      this.flash('error', this.translate.instant('STOCK.BONS.ERRORS.NO_LIGNES'));
      return;
    }

    if (this.isEdit() && this.editLignes.length === 0) {
      this.flash('error', this.translate.instant('STOCK.BONS.ERRORS.NO_LIGNES'));
      return;
    }

    const val = this.headerForm.value;
    const creating = this.isCreate();
    const req$ = creating
      ? this.buildCreateRequest$(val)
      : this.buildUpdateRequest$(val);

    req$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.flash('success', creating
          ? this.translate.instant('STOCK.BONS.SUCCESS.CREATED')
          : this.translate.instant('STOCK.BONS.SUCCESS.UPDATED'));
        this.cancel();
        this.reload();
      },
      error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('STOCK.BONS.ERRORS.OPERATION_FAILED')),
    });
  }

  private buildCreateRequest$(val: any): Observable<any> {
    const lignes = this.pendingLignes.map(l => ({
      articleId: l.articleId,
      quantity:  l.quantity,
      price:     l.price,
      remarque:  l.remarque ?? null,
    }));
    switch (this.activeBonType) {
      case 'entre':
        return this.stock.createBonEntre({
          fournisseurId: val.fournisseurId,
          observation:   val.observation || null,
          lignes,
        } as CreateBonEntreRequest);
      case 'sortie':
        return this.stock.createBonSortie({
          clientId:    val.clientId,
          observation: val.observation || null,
          lignes,
        } as CreateBonSortieRequest);
      default:
        return this.stock.createBonRetour({
          sourceId:    val.sourceId,
          sourceType:  val.sourceType,
          motif:       val.motif,
          observation: val.observation || null,
          lignes,
        } as CreateBonRetourRequest);
    }
  }

  private buildUpdateRequest$(val: any): Observable<any> {
    const id = this.selectedBon!.id;
    const lignes = this.selectedBon!.lignes.map((l: LigneResponseDto) => ({
      articleId: l.articleId,
      quantity:  l.quantity,
      price:     l.price,
      remarque:  l.remarque ?? null,
    }));
    switch (this.activeBonType) {
      case 'entre':
        return this.stock.updateBonEntre(id, {
          observation:   val.observation || null,
          fournisseurId: val.fournisseurId,
          lignes,
        } as UpdateBonEntreRequest);
      case 'sortie':
        return this.stock.updateBonSortie(id, {
          observation: val.observation || null,
          clientId:    val.clientId,
          lignes,
        } as UpdateBonSortieRequest);
      default:
        return this.stock.updateBonRetour(id, {
          motif:       val.motif,
          observation: val.observation || null,
          sourceId:    val.sourceId,
          lignes,
        } as UpdateBonRetourRequest);
    }
  }

  delete(bon: BonRecord): void {
    this.dialog
      .open(ModalComponent, {
        width: '400px',
        data: {
          title:       this.translate.instant('CONFIRMATION.DELETE_BON_TITLE'),
          message:     this.translate.instant('CONFIRMATION.DELETE_BON', { numero: bon.numero }),
          confirmText: this.translate.instant('COMMON.DELETE'),
          showCancel:  true,
          icon:        'auto_delete',
          iconColor:   'danger',
        },
      })
      .afterClosed()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        switchMap((confirmed) => {
          if (!confirmed) return EMPTY;
          return this.bonApi[this.activeBonType].delete(bon.id);
        }),
      )
      .subscribe({
        next: () => {
          if (this.isView()) this.cancel();
          this.reload();
          this.flash('success', this.translate.instant('STOCK.BONS.SUCCESS.DELETED', { numero: bon.numero }));
        },
        error: (err) => this.flash('error', (err.error as HttpError)?.message ?? this.translate.instant('STOCK.BONS.ERRORS.DELETE_FAILED')),
      });
  }

  asEntre(b: BonRecord):  BonEntreResponse  { return b as BonEntreResponse; }
  asSortie(b: BonRecord): BonSortieResponse { return b as BonSortieResponse; }
  asRetour(b: BonRecord): BonRetourResponse { return b as BonRetourResponse; }

  getLignes(b: BonRecord): LigneResponseDto[] {
    return b.lignes as LigneResponseDto[];
  }

  getTotal(b: BonRecord): number {
    return this.getLignes(b).reduce((s, l) => s + l.quantity * l.price, 0);
  }

  get editLignes(): LigneResponseDto[] {
    return this.selectedBon ? this.getLignes(this.selectedBon) : [];
  }

  trackByLocalId(_: number, l: PendingLigne): string { return l._localId; }

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
  trackById(_: number, b: BonRecord): string { return b.id; }
  setViewMode(mode: ViewMode): void { this.viewMode.set(mode); this.cdr.markForCheck(); }

  private flattenObject(obj: any): string {
    return Object.keys(obj).map(k => {
      const v = obj[k];
      return v && typeof v === 'object' ? this.flattenObject(v) : v;
    }).join(' ').toLowerCase();
  }

  private getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((acc, k) => acc?.[k], obj);
  }

  removeLigne(selectedBon: BonRecord, ligneId: string): void {
    selectedBon.lignes = (selectedBon.lignes as LigneResponseDto[]).filter(l => l.id !== ligneId);
    this.cdr.markForCheck();
  }

  private readonly bonApi: Record<BonType, BonApi> = {
    entre:  { list: (p, s) => this.stock.getBonEntres(p, s) as any, delete: id => this.stock.deleteBonEntre(id) },
    sortie: { list: (p, s) => this.stock.getBonSorties(p, s) as any, delete: id => this.stock.deleteBonSortie(id) },
    retour: { list: (p, s) => this.stock.getBonRetours(p, s) as any, delete: id => this.stock.deleteBonRetour(id) },
  };

  private syncArticles(): void {
    const consumed = new Map<string, number>();
    const editingId = this.inlineLigneLocalId;
    const activeLignes = this.isCreate()
      ? this.pendingLignes
      : (this.selectedBon?.lignes ?? []) as any[];
    for (const l of activeLignes) {
      const lid = (l as any)._localId ?? (l as any).id;
      if (lid === editingId) continue;
      const prev = consumed.get(l.articleId) ?? 0;
      consumed.set(l.articleId, prev + l.quantity);
    }
    this.articles = this.masterArticles
      .map(a => {
        let maxQty: number;
        if (this.activeBonType === 'sortie') {
          const warehouseStock = this.masterStockMap.get(a.id) ?? 0;
          const used = consumed.get(a.id) ?? 0;
          maxQty = warehouseStock - used;
        } else if (this.activeBonType === 'retour') {
          if (this.masterRetourMap.size === 0) {
            maxQty = Infinity;
          } else {
            const sourceMax = this.masterRetourMap.get(a.id) ?? 0;
            const used = consumed.get(a.id) ?? 0;
            maxQty = sourceMax - used;
          }
        } else {
          maxQty = Infinity;
        }
        return { ...a, _maxQty: maxQty };
      })
      .filter(a => {
        if (this.activeBonType === 'entre') return true;
        if (this.activeBonType === 'retour' && this.masterRetourMap.size === 0) return true;
        const lid = this.inlineLigneLocalId;
        const isEditing = lid && activeLignes.some(
          (l: any) => ((l as any)._localId ?? (l as any).id) === lid && l.articleId === a.id
        );
        return (a as any)._maxQty > 0 || isEditing;
      });
    this.cdr.markForCheck();
    if (this.articleDropdownOpen) {
      this.loadArticlesForDropdown(1, false);
    }
    console.log(this.articles);
  }

  getArticleMaxQty(articleId: string): number {
    const a = this.articles.find(x => x.id === articleId);
    return (a as any)?._maxQty ?? Infinity;
  }

  getAddButtonTooltip(): string {
    if (this.fournisseurs.length === 0 && this.activeBonType === 'entre')
      return this.translate.instant('STOCK.ERRORS.FOURNISSEURS_NOT_FOUND');
    if (this.articles.length === 0 && (this.activeBonType === 'retour' || this.activeBonType === 'sortie'))
      return this.translate.instant('STOCK.ERRORS.ARTICLES_NOT_FOUND');
    return '';
  }

  onClientSearch(query: string): void {
    this.clientSearchSubject$.next(query);
  }

  loadMoreClients(): void {
    if (!this.hasMoreClients || this.clientsLoading) return;
    this.loadClients(this.clientPage + 1, true);
  }

  selectClient(client: ClientResponseDto): void {
    this.headerForm.patchValue({ clientId: client.id });
    this.selectedClientLabel = `${client.name} - ${client.email}`;
    this.clientDropdownOpen = false;
    this.cdr.markForCheck();
  }

  private restoreClientLabel(): void {
    const clientId = this.headerForm.get('clientId')?.value;
    if (!clientId) return;
    const found = this.clients.find(c => c.id === clientId);
    if (found) {
      this.selectedClientLabel = `${found.name} - ${found.email}`;
      this.cdr.markForCheck();
    }
  }

  toggleClientDropdown(): void {
    this.clientDropdownOpen = !this.clientDropdownOpen;
    if (this.clientDropdownOpen) {
      this.clientSearchQuery = '';
      this.loadClients(1, false);
    }
  }

  private initFournisseurSearch(): void {
    this.fournisseurSearchSubject$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(query => {
        this.fournisseurSearchQuery = query;
        this.fournisseurPage = 1;
        this.hasMoreFournisseurs = true;
        this.loadFournisseurs(1, false);
      });
  }

  private initArticleSearch(): void {
    this.articleSearchSubject$
      .pipe(debounceTime(150), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(query => {
        this.articleSearchQuery = query;
        this.articlePage = 1;
        this.hasMoreArticles = true;
        this.loadArticlesForDropdown(1, false);
      });
  }

  loadFournisseurs(page: number, append = false): void {
    if (this.fournisseursLoading) return;
    this.fournisseursLoading = true;
    this.stock.getFournisseursPaged(page, this.fournisseurPageSize, this.fournisseurSearchQuery)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const items = res.items.filter(f => !f.isDeleted && !f.isBlocked);
          this.fournisseurs = append ? [...this.fournisseurs, ...items] : items;
          this.fournisseurTotalCount = this.fournisseurs.length;
          this.fournisseurPage = page;
          this.hasMoreFournisseurs = this.fournisseurs.length < res.totalCount;
          this.fournisseursLoading = false;
          if (!append && this.fournisseurs.length > 0 && !this.headerForm.get('fournisseurId')?.value) {
            this.selectFournisseur(this.fournisseurs[0]);
          }
          if (this.isEdit()) this.restoreFournisseurLabel();
          this.cdr.markForCheck();
        },
        error: () => { this.fournisseursLoading = false; this.cdr.markForCheck(); }
      });
  }

  loadMoreFournisseurs(): void {
    if (!this.hasMoreFournisseurs || this.fournisseursLoading) return;
    this.loadFournisseurs(this.fournisseurPage + 1, true);
  }

  selectFournisseur(fournisseur: FournisseurResponse): void {
    this.headerForm.patchValue({ fournisseurId: fournisseur.id });
    this.selectedFournisseurLabel = `${fournisseur.name} — ${fournisseur.taxNumber}`;
    this.fournisseurDropdownOpen = false;
    this.cdr.markForCheck();
  }

  toggleFournisseurDropdown(): void {
    this.fournisseurDropdownOpen = !this.fournisseurDropdownOpen;
    if (this.fournisseurDropdownOpen) {
      this.fournisseurSearchQuery = '';
      this.loadFournisseurs(1, false);
    }
  }

  onFournisseurSearch(query: string): void {
    this.fournisseurSearchSubject$.next(query);
  }

  private prefetchFournisseurs(ids: string[]): void {
    const unique = [...new Set(ids)];

    forkJoin(
      unique.map(id => this.stock.getFournisseurById(id).pipe(
        map(f => ({ id, name: f.name })),
        catchError(() => of({ id, name: '—' }))
      ))
    ).pipe(take(1))
    .subscribe(results => {
      results.forEach(r => this.fournisseurCache.set(r.id, r.name));
      this.fournisseurNames.set(new Map(this.fournisseurCache));
    });
  }

  getFournisseurName(id: string): string {
    if (this.fournisseurCache.has(id)) {
      return this.fournisseurCache.get(id)!;
    }

    // Fetch only if not cached
    this.stock.getFournisseurById(id)
      .pipe(take(1))
      .subscribe({
        next: (fournisseur) => {
          this.fournisseurCache.set(id, fournisseur.name);
          this.fournisseurNames.set(new Map(this.fournisseurCache)); // trigger signal update
        },
        error: () => {
          this.fournisseurCache.set(id, '—'); // fallback so it doesn't retry on every render
        }
      });

    return '…'; // placeholder while loading
  }

  private restoreFournisseurLabel(): void {
    const id = this.headerForm.get('fournisseurId')?.value;
    if (!id) return;
    const found = this.fournisseurs.find(f => f.id === id);
    if (found) {
      this.selectedFournisseurLabel = `${found.name} — ${found.taxNumber}`;
      this.cdr.markForCheck();
    }
  }

  loadArticlesForDropdown(page: number, append = false): void {
    const query = this.articleSearchQuery.toLowerCase();
    const filtered = this.articles.filter(a =>
      !query ||
      a.libelle?.toLowerCase().includes(query) ||
      a.codeRef?.toLowerCase().includes(query)
    );
    this.articleTotalCount = filtered.length;
    const start = (page - 1) * this.articlePageSize;
    const slice = filtered.slice(start, start + this.articlePageSize);
    this.articleDropdownItems = append ? [...this.articleDropdownItems, ...slice] : slice;
    this.articlePage = page;
    this.hasMoreArticles = this.articleDropdownItems.length < filtered.length;
    if (!append && this.articleDropdownItems.length > 0 && !this.ligneForm.get('articleId')?.value) {
      this.selectArticleForLigne(this.articleDropdownItems[0]);
    }
    this.cdr.markForCheck();
  }

  loadMoreArticles(): void {
    if (!this.hasMoreArticles || this.articlesLoading) return;
    this.loadArticlesForDropdown(this.articlePage + 1, true);
  }

  selectArticleForLigne(article: ArticleResponseDto): void {
    this.ligneForm.patchValue({ articleId: article.id, price: article.prix });
    this.selectedArticleLabel = `${article.codeRef} — ${article.libelle}`;
    this.articleDropdownOpen = false;

    const step = this.getArticleQtyStep(article.id);
    const max = this.getArticleMaxQty(article.id);

    this.ligneForm.setValidators([
      Validators.required,
      Validators.min(step),  // ← min matches step
      ...(max === Infinity ? [] : [Validators.max(max)]),
    ]);

    this.updateQuantityValidator(max === Infinity ? null : max);
    this.cdr.markForCheck();
  }

  toggleArticleDropdown(): void {
    this.articleDropdownOpen = !this.articleDropdownOpen;
    if (this.articleDropdownOpen) {
      this.articleSearchQuery = '';
      this.loadArticlesForDropdown(1, false);
    }
  }

  onArticleSearch(query: string): void {
    this.articleSearchSubject$.next(query);
  }

  getArticleQtyStep(articleId: string): number {
    const unit = this.masterArticles.find(a => a.id === articleId)?.unit as UnitEnum;
    if (!unit) return 1;

    switch (unit) {
      // High precision decimals
      case UnitEnum.Gram:
      case UnitEnum.Milligram:
      case UnitEnum.Milliliter:
      case UnitEnum.Millimeter:
        return 0.001;

      // Standard decimals
      case UnitEnum.Kilogram:
      case UnitEnum.Liter:
      case UnitEnum.Meter:
      case UnitEnum.Centimeter:
      case UnitEnum.CubicMeter:
        return 0.01;

      // Large units — whole or decimal
      case UnitEnum.Ton:
      case UnitEnum.Kilometer:
        return 0.001;

      // Whole numbers only
      case UnitEnum.Piece:
      case UnitEnum.Hour:
      case UnitEnum.Day:
      default:
        return 1;
    }
  }


  getClientName(id: string): string {
    return this.clients.find(c => c.id === id)?.name ?? id;
  }
}