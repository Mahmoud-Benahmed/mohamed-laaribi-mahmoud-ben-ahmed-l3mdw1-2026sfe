import { CommonModule, Location, ViewportScroller } from '@angular/common';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ClientResponseDto, ClientsService } from '../../../services/clients/clients.service';
import { StockItem, StockService } from '../../../services/stock.service';
import { AuthService } from '../../../services/auth/auth.service';
import { InvoiceDto, InvoiceService, TaxCalculationMode } from '../../../services/invoice.service';
import { ArticleResponseDto, ArticleService, UnitEnum } from '../../../services/articles/articles.service';
import { catchError, firstValueFrom, forkJoin, map, Observable, of, Subject, tap } from 'rxjs';
import { HttpError } from '../../../interfaces/HttpError';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomToggleComponent } from "../../toggle-slider/toggle-slider";
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyConfigService } from '../../../services/currency-config.service';

interface PendingItem {
  _localId: string;
  articleId: string;
  articleName: string;
  articleBarCode: string;
  quantity: number;
  uniPriceHT: number;       // original price before discount
  effectivePriceHT: number; // price after discount applied
  taxRate: number;
  totalHT: number;
  totalTTC: number;
  taxAmount: number;        // isolated tax amount for perInvoice mode
}

export interface InvoiceValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  discountedTotal?: number;
  originalTotal?: number;
  discountApplied?: number;
  discountRate?: number;
}

type CreditLimitInfo= {
    hasSufficientCredit: boolean,
    currentUsage: number,
    remainingCredit: number
};


@Component({
  selector: 'app-invoices-edit',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe,
    RouterLink
  ],
  templateUrl: './edit.html',
  styleUrl: './edit.scss',
})
export class EditInvoiceComponent implements OnInit, OnDestroy{
    private themeObserver: MutationObserver | null = null;
    private readonly destroyRef = inject(DestroyRef);
    private translate = inject(TranslateService);
    private cdr = inject(ChangeDetectorRef);
    private location= inject(Location);

  invoiceForm!: FormGroup;
  itemForm!: FormGroup;
  selectedInvoice: InvoiceDto | null = null;
  invoiceIdFromRoute: string|null=null;

  readonly TaxModes= TaxCalculationMode;

  selectedClientForValidation: ClientResponseDto | null = null;
  private _selectedArticle: StockItem | null = null;
  private masterArticles: StockItem[] = [];
  articles: StockItem[] = [];
  clients: ClientResponseDto[] = [];
  filteredClients: ClientResponseDto[] = [];
  clientSearchQuery = '';
  pendingItems: PendingItem[] = [];

  isValidating= false;
  taxCalculationMode: TaxCalculationMode= TaxCalculationMode.LINE;
  creditWarning: string | null = null;
  creditLimitInfo: CreditLimitInfo = {
    hasSufficientCredit: true,
    currentUsage: 0,
    remainingCredit: 0
  };
  discountInfo = {
    applies: false,
    rate: 0,
    discountAmount: 0,
    discountAmountHT: 0,
    originalTotal: 0,
    discountedTotal: 0
  };

  inlineItemLocalId = '';
  inlineItemOpen = false;

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null

  clientPage = 1;
  clientPageSize = 10;
  clientTotalCount = 0;
  clientsLoading = false;
  hasMoreClients = true;
  clientDropdownOpen = false;
  selectedClientLabel = '';
  private clientSearchSubject$ = new Subject<string>();

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

  constructor(
    public authService: AuthService,
    private invoiceService: InvoiceService,
    private articleService: ArticleService,
    private fb: FormBuilder,
    private stock: StockService,
    private route: ActivatedRoute,
    private currencyConfig: CurrencyConfigService,
  ) {}

  ngOnInit(): void {
    this.buildForms();
    this.invoiceIdFromRoute = this.route.snapshot.paramMap.get('id');

    if (!this.invoiceIdFromRoute) {
      this.cancel();
      return;
    }

    this.reload();
  }

  get currencyCode():   string { return this.currencyConfig.code;   }
  get currencyLocale(): string { return this.currencyConfig.locale; }

  private populateFormFromInvoice(): void {
    if (!this.selectedInvoice) return;

    // Find the client from the loaded clients list
    const client = this.clients.find(c => c.id === this.selectedInvoice!.clientId);
    if (client) {
      this.selectedClientForValidation = client;
      this.clientSearchQuery = client.name;
    }

    this.taxCalculationMode = this.selectedInvoice.taxMode === 'INVOICE' ? TaxCalculationMode.INVOICE
                                                                          : TaxCalculationMode.LINE;

    // Patch invoice form
    this.invoiceForm.patchValue({
      invoiceDate: this.selectedInvoice.invoiceDate?.split('T')[0] || '',
      dueDate: this.selectedInvoice.dueDate?.split('T')[0] || '',
      clientId: this.selectedInvoice.clientId,
      clientFullName: this.selectedInvoice.clientFullName,
      clientAddress: this.selectedInvoice.clientAddress || '',
      additionalNotes: this.selectedInvoice.additionalNotes || '',
      taxModeInvoice: this.selectedInvoice.taxMode || this.TaxModes.INVOICE,
    });

    // Build pending items from invoice items
    this.pendingItems = this.selectedInvoice.items.map(item => {
      const taxRate   = item.taxRate * 100;
      const totalHT   = Math.round(item.quantity * item.uniPriceHT * 100) / 100;
      const taxAmount = Math.round(totalHT * (taxRate / 100) * 100) / 100;
      const totalTTC  = Math.round((totalHT + taxAmount) * 100) / 100;
      return {
        _localId: crypto.randomUUID(),
        articleId: item.articleId,
        articleName: item.articleName,
        articleBarCode: item.articleBarCode,
        quantity: item.quantity,
        uniPriceHT: item.uniPriceHT,
        effectivePriceHT: item.uniPriceHT,
        taxRate,
        totalHT,
        taxAmount,
        totalTTC,
      };
    });

    // Update derived data
    this.syncArticles();
    this.refreshCreditAndDiscount();

    // Mark form as pristine initially (no unsaved changes)
    this.invoiceForm.markAsPristine();
    this.cdr.detectChanges();
  }

  private buildForms(): void {
    this.invoiceForm = this.fb.group({
      invoiceDate:     ['', Validators.required],
      dueDate:         ['', Validators.required],
      clientId:        ['', Validators.required],
      clientFullName:  ['', Validators.required],
      clientAddress:   ['', Validators.required],
      additionalNotes: [null],
      taxModeInvoice:  [false], // false = perLine, true = perInvoice
    });

    this.itemForm = this.fb.group({
      articleId:  ['', Validators.required],
      quantity:   [1,  [Validators.required, Validators.min(1)]],
      uniPriceHT: [0,  [Validators.required, Validators.min(0)]],
      taxRate:    [19, [Validators.required, Validators.min(0), Validators.max(100)]],
    });

    this.invoiceForm.get('invoiceDate')?.valueChanges.subscribe(() => {
      this.onInvoiceDateChange();
    });

    // Clear cached selected article when articleId changes
    this.itemForm.get('articleId')?.valueChanges.subscribe(id => {
      this._selectedArticle = id ? (this.articles.find(a => a.id === id) ?? null) : null;
    });
  }

  // service load calls
  loadClients(page: number, append = false): void {
    if (this.clientsLoading) return;
    this.clientsLoading = true;

    this.invoiceService.getClientsPaged(page, this.clientPageSize, this.clientSearchQuery)
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
          // Auto-select first client if nothing selected yet
          if (!append && this.clients.length > 0 && !this.invoiceForm.get('clientId')?.value) {
            this.selectClient(this.clients[0]);
          }
          this.cdr.markForCheck();
        },
        error: () => {
          this.clientsLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  loadArticlesWithStock(): Observable<StockItem[]> {
    return forkJoin({
      articles: this.articleService.getAll(1, 1000).pipe(catchError(() => of({ items: [] }))),
      stock: this.stock.getStockArticles().pipe(catchError(() => of({ inStock: [], outStock: [] })))
    }).pipe(
      map((results) => {
        const allArticles = results.articles.items || [];
        const stockData = results.stock || { inStock: [], outStock: [] };

        const stockMap = new Map<string, number>();
        stockData.inStock.forEach((s: any) => {
          stockMap.set(s.articleId || s.id, s.quantity);
        });
        // ✅ Include all articles, even if stock is 0
        this.masterArticles = allArticles.map((a: any) => ({
          ...a,
          quantity: stockMap.get(a.id) ?? 0
        }));

        this.syncArticles();
        return this.articles;
      }),
      catchError(() => {
        this.syncArticles();
        this.articles = [];
        return of([]);
      })
    );
  }

  loadInvoice(invoiceId: string): Observable<InvoiceDto> {
    return this.invoiceService.getById(invoiceId).pipe(
        tap({
          next: (invoice) => {
            this.selectedInvoice = invoice;
          },
          error: (err) => {
            const errorMsg = (err.error as HttpError)?.message;
            this.flash('error', errorMsg);
            this.isValidating = false;

            setTimeout(() => {
              const el = document.getElementById('top');
              el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }, 0);
          }
        })
    );
  }

  reload(): void {
    forkJoin({
      selectedInvoice: this.loadInvoice(this.invoiceIdFromRoute!),
      articles: this.loadArticlesWithStock(),
    }).subscribe({
      next: (res) => {
          this.loadClients(1, false);
          this.populateFormFromInvoice();
          this.cdr.markForCheck();
      },
      error: (err) => {
        const errorMsg = (err.error as HttpError)?.message;
        this.flash('error', errorMsg);
        this.isValidating = false;

        setTimeout(() => {
          const el = document.getElementById('top');
          el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0);
        this.cdr.markForCheck();
      }
    });
  }


  invoiceTotalTTC = computed(() => {
    if (this.discountInfo.applies && this.discountInfo.discountedTotal > 0) {
      return this.discountInfo.discountedTotal;
    }
    return this.pendingTotalTTC;
  });


  private syncArticles(): void {
    const consumed = new Map<string, number>();
    for (const item of this.pendingItems) {
      if (item._localId === this.inlineItemLocalId) continue;
      consumed.set(item.articleId, (consumed.get(item.articleId) ?? 0) + item.quantity);
    }

    const editingId = this._selectedArticle?.id ?? null;

    this.articles = this.masterArticles
      .map(master => ({
        ...master,
        quantity: master.quantity - (consumed.get(master.id) ?? 0),
      }))
      .filter(a => a.quantity > 0 || a.id === editingId);

    this.cdr.markForCheck();
    this.refreshCreditAndDiscount();
  }

  // Form helpers
  getSelectedArticle(): StockItem | null {
    return this._selectedArticle;
  }

  getStockStatusClass(availableStock: number): string {
    if (availableStock === 0) return 'stock-out';
    if (availableStock <= 5) return 'stock-critical';
    if (availableStock <= 10) return 'stock-low';
    return 'stock-normal';
  }

  isLowStock(stock: number): boolean { return stock > 0 && stock <= 10; }
  isCriticalStock(stock: number): boolean { return stock > 0 && stock <= 5; }
  isOutOfStock(stock: number): boolean { return stock === 0; }

  getAddButtonTooltip(): string {
    return this.articles.length === 0 ? this.translate.instant('stock.responses.errors.ARTICLES_NOT_FOUND') : '';
  }

  checkArticleStock(articleId: string, _requestedQuantity: number): void {
    const article = this.articles.find(a => a.id === articleId);
    this.updateQuantityValidator(article?.quantity ?? 0);
  }

  getQuantityStep(unit?: string): number {
    if (!unit) return 1;
    const integerUnits = [UnitEnum.Piece, UnitEnum.Hour, UnitEnum.Day];
    return integerUnits.includes(unit as UnitEnum) ? 1 : 0.1;
  }

  getQuantityMin(unit?: string): number {
    if (!unit) return 1;
    const integerUnits = [UnitEnum.Piece, UnitEnum.Hour, UnitEnum.Day];
    return integerUnits.includes(unit as UnitEnum) ? 1 : 0.001;
  }

  getUnitTranslation(): string {
    const unit = this._selectedArticle?.unit;
    if (!unit) return '';
    return this.translate.instant(`common.unit.${unit.toUpperCase()}`);
  }

  getAvailableStock(articleId: string): number {
    return this.articles.find(a => a.id === articleId)?.quantity || 0;
  }
  onArticleSelectChange(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    const article = this.articles.find(a => a.id === id);
    this.onArticleSelected(article);
  }

  onArticleSelected(article: StockItem | undefined): void {
    if (!article) return;
    this._selectedArticle = article;
    this.itemForm.patchValue({
      uniPriceHT: article.prix ?? 0,
      taxRate: article.tva ?? 19,
    });
    this.updateQuantityValidator(article.quantity ?? 0);
  }

  openInlineItemAdd(): void {
    this.itemForm.reset({ articleId: '', quantity: 1, uniPriceHT: 0, taxRate: 19 });
    this._selectedArticle = null;
    this.inlineItemLocalId = '';
    this.inlineItemOpen = true;
  }

  openInlineItemEdit(item: PendingItem): void {
    this.inlineItemLocalId = item._localId;  // set BEFORE syncArticles
    this._selectedArticle = this.masterArticles.find(a => a.id === item.articleId) ?? null;
    this.syncArticles();                     // now the editing line is excluded from consumed
    this.itemForm.patchValue({
      articleId:  item.articleId,
      quantity:   item.quantity,
      uniPriceHT: item.uniPriceHT,
      taxRate:    item.taxRate,
    });
    if (this._selectedArticle) this.updateQuantityValidator(this._selectedArticle.quantity);
    this.inlineItemOpen = true;
  }

  closeInlineItem(): void {
    this.inlineItemOpen = false;
    this.inlineItemLocalId = '';
    this._selectedArticle = null;
    this.itemForm.reset();
    this.syncArticles();
    this.isValidating = false; // ← add this
  }

  submitInlineItem(): void {
    if (this.itemForm.invalid) {
      this.flash('error', this.translate.instant('validation.required'));
      return;
    }

    const { articleId, quantity, uniPriceHT, taxRate } = this.itemForm.value;

    // Use masterArticles for the real stock ceiling
    const master = this.masterArticles.find(a => a.id === articleId);
    if (!master) {
      this.flash('error', this.translate.instant('errors.article_not_found'));
      return;
    }

    // Total already consumed by OTHER pending lines (excluding the one being edited)
    const alreadyConsumed = this.pendingItems
      .filter(i => i.articleId === articleId && i._localId !== this.inlineItemLocalId)
      .reduce((sum, i) => sum + i.quantity, 0);

    const maxAllowed = master.quantity - alreadyConsumed;

    if (quantity > maxAllowed) {
      this.flash('error', this.translate.instant('stock.responses.errors.INSUFFICIENT_STOCK', {
        max: maxAllowed, requested: quantity
      }));
      return;
    }

  const discountRate = this.discountInfo.applies ? this.discountInfo.rate : 0;

  const { effectivePriceHT, totalHT, taxAmount, totalTTC } =
    this.calcLineAmounts(quantity, uniPriceHT, taxRate, discountRate);

  if (this.inlineItemLocalId) {
    const idx = this.pendingItems.findIndex(i => i._localId === this.inlineItemLocalId);
    if (idx !== -1) {
      this.pendingItems[idx] = {
        ...this.pendingItems[idx],
        articleId, articleName: master.libelle ?? '',
        articleBarCode: master.barCode ?? '',
        quantity, uniPriceHT, effectivePriceHT,
        taxRate, totalHT, taxAmount, totalTTC,
      };
    }
  } else {
    const existingIndex = this.pendingItems.findIndex(i => i.articleId === articleId);
    if (existingIndex !== -1) {
      const existing = this.pendingItems[existingIndex];
      const newQuantity = existing.quantity + quantity;
      if (newQuantity > maxAllowed) { return; }

      const merged = this.calcLineAmounts(newQuantity, existing.uniPriceHT, existing.taxRate, discountRate);
      this.pendingItems[existingIndex] = {
        ...existing, quantity: newQuantity,
        effectivePriceHT: merged.effectivePriceHT,
        totalHT: merged.totalHT, taxAmount: merged.taxAmount, totalTTC: merged.totalTTC,
      };
    } else {
      this.pendingItems.push({
        _localId: crypto.randomUUID(),
        articleId, articleName: master.libelle ?? '',
        articleBarCode: master.barCode ?? '',
        quantity, uniPriceHT, effectivePriceHT,
        taxRate, totalHT, taxAmount, totalTTC,
      });
    }
  }

    this.pendingItems = [...this.pendingItems];
    this.invoiceForm.markAsDirty();
    this.closeInlineItem();
    this.syncArticles();
    this.refreshCreditAndDiscount();
  }

  private calcLineAmounts(
    qty: number,
    uniPriceHT: number,
    taxRate: number,
    discountRate = 0
  ): { effectivePriceHT: number; totalHT: number; taxAmount: number; totalTTC: number } {
    const effectivePriceHT = Math.round(uniPriceHT * (1 - discountRate / 100) * 100) / 100;
    const totalHT           = Math.round(qty * effectivePriceHT * 100) / 100;
    const taxAmount         = Math.round(totalHT * (taxRate / 100) * 100) / 100;
    const totalTTC          = Math.round((totalHT + taxAmount) * 100) / 100;
    return { effectivePriceHT, totalHT, taxAmount, totalTTC };
  }

  async refreshCreditAndDiscount(): Promise<void> {
      const client = this.selectedClientForValidation;
      if (!client || this.pendingItems.length === 0) {
        this.creditWarning = null;
        this.discountInfo = { applies: false, rate: 0, discountAmount: 0, discountAmountHT: 0, originalTotal: 0, discountedTotal: 0 };
        this.creditLimitInfo = { hasSufficientCredit: true, currentUsage: 0, remainingCredit: Infinity };
        return;
      }

      const { discountRate, applies } = this.invoiceService.calculateBulkDiscount(client);
      const originalTotalHT  = this.pendingItems.reduce((s, i) => s + i.quantity * i.uniPriceHT, 0);
      const originalTotalTTC = this.pendingItems.reduce((s, i) => s + i.quantity * i.uniPriceHT * (1 + i.taxRate / 100), 0);
      const discountMultiplier = 1 - discountRate / 100;
      const discountedTotalHT  = originalTotalHT * discountMultiplier;
      const discountedTotalTTC = originalTotalTTC * discountMultiplier;

      this.discountInfo = {
        applies, rate: discountRate,
        discountAmountHT: Math.round((originalTotalHT - discountedTotalHT) * 100) / 100,
        discountAmount:   Math.round((originalTotalTTC - discountedTotalTTC) * 100) / 100,
        originalTotal:    Math.round(originalTotalTTC * 100) / 100,
        discountedTotal:  Math.round(discountedTotalTTC * 100) / 100,
      };

      const finalTotal = applies ? discountedTotalTTC : originalTotalTTC;

      try {
        const outstanding = await firstValueFrom(this.invoiceService.getClientOutstandingBalance(client.id)); // ← ONE call
        const creditCheck = this.invoiceService.validateCreditLimit(client, finalTotal, outstanding || 0);

        this.creditLimitInfo = {
          hasSufficientCredit: creditCheck.hasSufficientCredit,
          currentUsage: creditCheck.currentUsage,
          remainingCredit: creditCheck.remainingCredit,
        };
        this.creditWarning = creditCheck.hasSufficientCredit ? null : creditCheck.message;
      } catch {
        this.creditLimitInfo = { hasSufficientCredit: false, currentUsage: 0, remainingCredit: 0 };
        this.creditWarning = 'Error while checking credit';
      }
  }

  get duePaymentPeriodHint(): string {
    if (!this.selectedClientForValidation?.duePaymentPeriod) return '';
    return this.translate.instant('invoices.form.due_date_hint', {
      days: this.selectedClientForValidation.duePaymentPeriod
    });
  }

  filterClients(query: string): void {
    if (!query || query.length < 2) { this.filteredClients = []; return; }
    const q = query.toLowerCase();
    this.filteredClients = this.clients
      .filter(c => c.name?.toLowerCase().includes(q) || c.email?.toLowerCase().includes(q))
      .slice(0, 8);
  }


  calculateDueDate(invoiceDate: string | Date | null | undefined, paymentPeriod: number | null | undefined): string {
    const daysToAdd = paymentPeriod || 30;
    const date = invoiceDate ? new Date(invoiceDate) : new Date();
    const due = new Date(date);
    due.setDate(date.getDate() + daysToAdd);
    return due.toISOString().split('T')[0];
  }
  onInvoiceDateChange(): void {
    const invoiceDate = this.invoiceForm.get('invoiceDate')?.value;
    if (this.selectedClientForValidation && invoiceDate) {
      this.invoiceForm.patchValue({
        dueDate: this.calculateDueDate(invoiceDate, this.selectedClientForValidation.duePaymentPeriod),
      });
    }
  }

  private updateQuantityValidator(maxStock: number): void {
    const qtyControl = this.itemForm.get('quantity');
    if (!qtyControl) return;
    const validators = [Validators.required, Validators.min(1)];
    if (maxStock > 0) validators.push(Validators.max(maxStock));
    qtyControl.setValidators(validators);
    qtyControl.updateValueAndValidity();
  }
    removePendingItem(localId: string): void {
      this.pendingItems = this.pendingItems.filter(i => i._localId !== localId);
      this.syncArticles();
      this.refreshCreditAndDiscount();
    }

  get canSubmit(): boolean {
    if (this.invoiceForm.invalid) return false;
    if (this.pendingItems.length === 0) return false;
    if (this.creditWarning) return false;
    if (this.isValidating) return false;
    if(!this.creditLimitInfo.hasSufficientCredit && this.selectedClientForValidation?.creditLimit) return false;

    return true;
  }

  getSubmitButtonTooltip(): string {
    if(!this.creditLimitInfo.hasSufficientCredit && this.selectedClientForValidation?.creditLimit) return this.translate.instant('invoices.responses.errors.insufficient_credit', {
        creditLimit: this.selectedClientForValidation.creditLimit.toFixed(2),
        currentOutstanding: this.creditLimitInfo.currentUsage.toFixed(2),
        invoiceTotal: this.invoiceTotalTTC()
      });
    if (this.isValidating) return this.translate.instant('common.processing');
    if (this.invoiceForm.invalid) return this.translate.instant('validation.required');
    if (this.pendingItems.length === 0) return this.translate.instant('invoices.form.no_items_yet');
    if (this.creditWarning) return this.translate.instant('invoices.responses.errors.credit_limit_exceeded');
    return '';
  }

  async validateAllItemsStock(): Promise<boolean> {
    const stockChecks = this.pendingItems.map(async (item) => {
      try {
        const response = await firstValueFrom(this.stock.getArticleCurrentStock(item.articleId));
        const currentStock = response?.currentStock ?? 0;
        if (currentStock < item.quantity) {
          return { valid: false, articleName: item.articleName, requested: item.quantity, available: currentStock };
        }
        return { valid: true };
      } catch {
        return { valid: true };
      }
    });

    const results = await Promise.all(stockChecks);
    const failures = results.filter(r => !r.valid);

    if (failures.length > 0) {
      const msgs = failures.map(f =>
        this.translate.instant('stock.responses.errors.INSUFFICIENT_STOCK_DETAIL', {
          article: (f as any).articleName,
          requested: (f as any).requested,
          available: (f as any).available,
        })
      );
      this.flash('error', msgs.join('; '));
      return false;
    }
    return true;
  }

  // ── CRUD actions ──────────────────────────────────────────────────────────
  finalize(invoice: InvoiceDto): void {
    this.invoiceService.finalize(invoice.id).subscribe({
      next: (updated) => {
        if (this.selectedInvoice?.id === updated.id) {
          this.selectedInvoice = { ...updated }; // spread to trigger reference change
          this.cdr.markForCheck();
        }
        this.flash('success', this.translate.instant('invoices.responses.success.finalized'));
        setTimeout(() => {
          const el = document.getElementById('top');
          el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0); // wait for DOM update

        setTimeout(() => {
          this.isValidating = false;
          this.cancel();
        }, 2000);
        this.reload();
      },
      error: (err) =>{
        const errorMsg = (err.error as HttpError)?.message
          || this.translate.instant('invoices.responses.errors.finalize_failed');
        this.flash('error', errorMsg);
        this.isValidating = false;

        setTimeout(() => {
          const el = document.getElementById('top');
          el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0);
      }
    });
  }

  updateInvoice(): void {
    if (this.invoiceForm.invalid) {
      this.flash('error', this.translate.instant('validation.required'));
      return;
    }
    if (this.pendingItems.length === 0) {
      this.flash('error', this.translate.instant('invoices.form.no_items_yet'));
      return;
    }

    this.isValidating = true;
    const formValue = this.invoiceForm.value;

    const updateDto = {
      invoiceDate: formValue.invoiceDate,
      dueDate: formValue.dueDate,
      clientId: formValue.clientId,
      clientAddress: formValue.clientAddress,
      additionalNotes: formValue.additionalNotes,
      taxMode: this.taxCalculationMode,
      items: this.pendingItems.map(item => ({
          articleId:  item.articleId,
          quantity:   Number(item.quantity),
          uniPriceHT: Number(item.uniPriceHT),  // ✅ original — backend applies discount
          taxRate:    Number(item.taxRate / 100),
        }))
    };

    this.invoiceService.update(this.selectedInvoice!.id, updateDto).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('invoices.responses.success.updated'));

        setTimeout(() => {
          const el = document.getElementById('top');
          el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0); // wait for DOM update

        setTimeout(() => {
          this.isValidating = false;
          this.cancel();
        }, 2000);
      },
      error: (err) => {
        const errorMsg = (err.error as HttpError)?.message
          || this.translate.instant('invoices.responses.errors.update_failed');
        this.flash('error', errorMsg);
        this.isValidating = false;

        setTimeout(() => {
          const el = document.getElementById('top');
          el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0);
      },
    });
  }

    // Toggle handler (add to component):
  setTaxCalculationMode(mode: TaxCalculationMode): void {
    this.taxCalculationMode = mode;
    this.refreshCreditAndDiscount();
  }

  get pendingTotalHT(): number {
    return Math.round(this.pendingItems.reduce((s, i) => s + i.totalHT, 0) * 100) / 100;
  }

  get pendingTotalTVA(): number {
    if (this.taxCalculationMode === TaxCalculationMode.LINE) {
      return Math.round(this.pendingItems.reduce((s, i) => s + i.taxAmount, 0) * 100) / 100;
    }
    const totalHT = this.pendingTotalHT;
    if (totalHT === 0) return 0;
    const weightedRate = this.pendingItems.reduce(
      (s, i) => s + i.totalHT * (i.taxRate / 100), 0
    ) / totalHT;
    return Math.round(totalHT * weightedRate * 100) / 100;
  }

  get pendingTotalTTC(): number {
    return Math.round((this.pendingTotalHT + this.pendingTotalTVA) * 100) / 100;
  }

  trackById(_: number, item: { id: string }) { return item.id; }
  trackByLocalId(_: number, item: PendingItem) { return item._localId; }


  onClientSearch(query: string): void {
    this.clientSearchSubject$.next(query);
  }

  loadMoreClients(): void {
    if (!this.hasMoreClients || this.clientsLoading) return;
    this.loadClients(this.clientPage + 1, true);
  }

  selectClient(client: ClientResponseDto): void {
    this.selectedClientForValidation = client;

    // Mark form as dirty to trigger unsaved changes detection
    this.invoiceForm.markAsDirty();

    this.clientSearchQuery = client.name;
    this.filteredClients = [];
    this.refreshCreditAndDiscount();



    this.invoiceForm.patchValue({ clientId: client.id });
    this.selectedClientLabel = `${client.name} - ${client.email}`;
    this.clientDropdownOpen = false;
    this.selectedClientForValidation = client;

    let invoiceDate = this.invoiceForm.get('invoiceDate')?.value;
    if (!invoiceDate) {
      invoiceDate = new Date().toISOString().split('T')[0];
      this.invoiceForm.patchValue({ invoiceDate });
    }

    this.invoiceForm.patchValue({
      clientId: client.id,
      clientFullName: client.name,
      clientAddress: client.address ?? '',
      dueDate: this.calculateDueDate(invoiceDate, client.duePaymentPeriod),
    });

    // Mark form as dirty to trigger unsaved changes detection
    this.invoiceForm.markAsDirty();

    this.clientSearchQuery = client.name;
    this.filteredClients = [];
    this.refreshCreditAndDiscount()
    this.loadArticlesForDropdown(true);
    this.cdr.markForCheck();
  }

  private restoreClientLabel(): void {
    const clientId = this.invoiceForm.get('clientId')?.value;
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


  loadArticlesForDropdown(resetPage = true): void {
      if (resetPage) {
        this.articlePage = 1;
      }

      const creditLimit = this.selectedClientForValidation?.creditLimit;

      let filtered = this.articles.filter(a => {
        const matchesSearch =
          a.libelle?.toLowerCase().includes(this.articleSearchQuery.toLowerCase()) ||
          a.codeRef?.toLowerCase().includes(this.articleSearchQuery.toLowerCase()) ||
          a.barCode?.toLowerCase().includes(this.articleSearchQuery.toLowerCase());

        const withinCreditLimit = creditLimit == null || a.prix <= creditLimit;

        return matchesSearch && withinCreditLimit;
      });

      this.articleTotalCount = filtered.length;
      this.hasMoreArticles = this.articlePage * this.articlePageSize < this.articleTotalCount;

      const start = (this.articlePage - 1) * this.articlePageSize;
      const end = start + this.articlePageSize;
      this.articleDropdownItems = filtered.slice(start, end);

      this.cdr.markForCheck();
  }


  selectArticleForLigne(article: ArticleResponseDto): void {
    // Find the same article in this.articles to get the available stock
    const stockArticle = this.articles.find(a => a.id === article.id);
    if (!stockArticle) return;

    this.itemForm.patchValue({
      articleId: article.id,
      uniPriceHT: article.prix ?? 0,
      taxRate: article.tva ?? 19
    });

    this.selectedArticleLabel = `${article.codeRef} — ${article.libelle}`;
    this.articleDropdownOpen = false;

    // Set max quantity validator based on available stock
    this.updateQuantityValidator(stockArticle.quantity ?? 0);
    this.cdr.markForCheck();
  }

  toggleArticleDropdown(): void {
    if (!this.articleDropdownOpen) {
      // Reset search and pagination when opening
      this.articleSearchQuery = '';
      this.articlePage = 1;
      this.loadArticlesForDropdown(true);
    }
    this.articleDropdownOpen = !this.articleDropdownOpen;
  }

  onArticleSearch(query: string): void {
    this.articleSearchQuery = query;
    this.loadArticlesForDropdown(true);  // reset to first page
  }

  getArticleMaxQty(articleId: string): number {
    const a = this.articles.find(x => x.id === articleId);
    return (a as any)?._maxQty ?? Infinity;
  }

  loadMoreArticles(): void {
    if (!this.hasMoreArticles || this.articlesLoading) return;
    this.articlePage++;
    this.loadArticlesForDropdown(false);  // append mode
  }

  getArticleLabel(articleId: string): string {
    const article = this.masterArticles.find(a => a.id === articleId);
    return article ? `${article.codeRef} — ${article.libelle}` : articleId;
  }


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

  cancel(){
    this.location.back();
  }
  ngOnDestroy(): void {
    this.themeObserver?.disconnect();
  }
}