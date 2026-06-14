import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, firstValueFrom } from 'rxjs';
import { environment } from '../environment';
import { TranslateService } from '@ngx-translate/core';
import { ArticleResponseDto } from './articles/articles.service';
import { ClientResponseDto } from './clients/clients.service';

// ── DTOs ─────────────────────────────────────────────

export enum TaxCalculationMode{
  LINE, INVOICE
}
export interface InvoiceItemDto {
  id: string;
  articleId: string;
  articleName: string;
  articleBarCode: string;
  quantity: number;
  uniPriceHT: number;
  taxRate: number;
  totalHT: number;
  totalTTC: number;
}

export interface InvoiceDto {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  taxMode: string;
  dueDate: string;
  totalHT: number;
  totalTVA: number;
  totalTTC: number;
  status: 'DRAFT' | 'UNPAID' | 'PAID' | 'CANCELLED';
  clientId: string;
  clientFullName: string;
  clientAddress: string;
  additionalNotes: string | null;
  items: InvoiceItemDto[];
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateInvoiceDto {
  invoiceDate: string;
  dueDate: string;
  clientId: string;
  taxMode: TaxCalculationMode;
  additionalNotes: string | null;
  items: Array<{
    articleId: string;
    quantity: number;
    uniPriceHT: number;
    taxRate: number;
  }>;
}

export interface ClientRevenueDto {
  clientId: string;
  clientFullName: string;
  invoiceCount: number;
  revenueTTC: number;
}

export interface MonthlyStatsDto {
  year: number;
  month: number;
  issuedCount: number;
  paidCount: number;
  issuedTTC: number;
  paidTTC: number;
}

export interface InvoiceStatsDto {
  totalInvoices: number;
  draftCount: number;
  unpaidCount: number;
  paidCount: number;
  cancelledCount: number;
  deletedCount: number;
  overdueCount: number;
  totalRevenueHT: number;
  totalRevenueTTC: number;
  totalTVACollected: number;
  outstandingHT: number;
  outstandingTTC: number;
  overdueHT: number;
  overdueTTC: number;
  averageInvoiceValueHT: number;
  averagePaymentDays: number;
  topClients: ClientRevenueDto[];
  monthlyBreakdown: MonthlyStatsDto[];
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
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

export interface UpdateInvoiceDto extends CreateInvoiceDto{}

// ── SERVICE ──────────────────────────────────────────

@Injectable({
  providedIn: 'root'
})
export class InvoiceService {
  // Uses the dedicated invoice microservice on port 5037
  private readonly baseUrl = `${environment.routes.invoices}`;

  constructor(private readonly http: HttpClient,
              private readonly translate: TranslateService) { }


  // ── Helpers ───────────────────────────────────────
  private t(key: string, params?: any): string {
    return this.translate.instant(key, params);
  }

  private paginate<T>(items: T[], page: number, size: number): PagedResultDto<T> {
    const start = (page - 1) * size;
    return { items: items.slice(start, start + size), totalCount: items.length };
  }

  // ── GET ────────────────────────────────────────────
  getAll(pageNumber = 1, pageSize = 10, includeDeleted=false): Observable<PagedResultDto<InvoiceDto>> {
    const params= new HttpParams().set('pageNumber', pageNumber)
                                  .set('pageSize', pageSize)
                                  .set('includeDeleted', includeDeleted);

    return this.http.get<PagedResultDto<InvoiceDto>>(this.baseUrl, {params});
  }

  getByStatus(status: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<InvoiceDto>> {
      return this.http.get<PagedResultDto<InvoiceDto>>(`${this.baseUrl}/status/${status}`, {
          params: new HttpParams()
              .set('pageNumber', pageNumber)
              .set('pageSize', pageSize)
      });
  }

  getByClientId(clientId: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<InvoiceDto>> {
    return this.http.get<PagedResultDto<InvoiceDto>>(
      `${this.baseUrl}/client/${clientId}`, {
        params: new HttpParams()
        .set('pageNumber', pageNumber)
        .set('pageSize', pageSize)
      });
  }

  getById(id: string): Observable<InvoiceDto> {
    return this.http.get<InvoiceDto>(`${this.baseUrl}/${id}`);
  }

  getClientTotalTTC(clientId: string): Observable<number> {
      return this.http.get<PagedResultDto<InvoiceDto>>(
          `${this.baseUrl}/client/${clientId}`,
          { params: new HttpParams().set('pageNumber', 1).set('pageSize', 1000) }
      ).pipe(
          map(res => (res.items ?? [])
              .reduce((sum, i) => sum + i.totalTTC, 0)
          )
      );
  }

  getClientOutstandingBalance(clientId: string): Observable<number> {
      return this.http.get<PagedResultDto<InvoiceDto>>(
          `${this.baseUrl}/client/${clientId}`,
          { params: new HttpParams().set('pageNumber', 1).set('pageSize', 1000) }
      ).pipe(
          map(res => (res.items ?? [])
              .filter(i => i.status === 'UNPAID')
              .reduce((sum, i) => sum + i.totalTTC, 0)
          )
      );
  }

  getStats(topClientsCount = 5): Observable<InvoiceStatsDto> {
    const params = new HttpParams().set('topClientsCount', topClientsCount);
    return this.http.get<InvoiceStatsDto>(`${this.baseUrl}/stats`, { params });
  }

  // ── CREATE ─────────────────────────────────────────

  create(dto: CreateInvoiceDto): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(this.baseUrl, dto);
  }

  // ── ITEM MANAGEMENT ────────────────────────────────

  addItem(invoiceId: string, item: any): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(`${this.baseUrl}/${invoiceId}/items`, item);
  }

  removeItem(invoiceId: string, itemId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${invoiceId}/items/${itemId}`);
  }

  // ── LIFECYCLE ──────────────────────────────────────

  finalize(invoiceId: string): Observable<InvoiceDto> {
    return this.http.put<InvoiceDto>(`${this.baseUrl}/${invoiceId}/finalize`, {});
  }

  markAsPaid(invoiceId: string): Observable<InvoiceDto> {
    return this.http.put<InvoiceDto>(`${this.baseUrl}/${invoiceId}/pay`, {});
  }

  cancel(invoiceId: string): Observable<InvoiceDto> {
    return this.http.put<InvoiceDto>(`${this.baseUrl}/${invoiceId}/cancel`, {});
  }

  // ── DELETE / RESTORE ───────────────────────────────

  delete(invoiceId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${invoiceId}`);
  }

  restore(invoiceId: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${invoiceId}/restore`, {});
  }

  // ── DISCOUNT & VALIDATION METHODS ──────────────────

  // Calculate discount from client categories
  calculateBulkDiscount(client: any): { discountRate: number; applies: boolean } {
    if (!client.categories || client.categories.length === 0) {
      return { discountRate: 0, applies: false };
    }

    const bulkCategories = client.categories.filter((cat: any) => cat.useBulkPricing === true);

    if (bulkCategories.length === 0) {
      return { discountRate: 0, applies: false };
    }

    const highestDiscount = Math.max(...bulkCategories.map((cat: any) => cat.discountRate || 0));

    // Normalize: if stored as decimal (0–1), convert to percentage (0–100)
    const normalizedRate = highestDiscount <= 1 ? highestDiscount * 100 : highestDiscount;

    return {
      discountRate: normalizedRate,
      applies: normalizedRate > 0
    };
  }

  // Apply discount to items
  applyDiscountToItems(
    items: CreateInvoiceDto['items'],
    discountRate: number
  ): CreateInvoiceDto['items'] {
    if (discountRate <= 0) return items;

    const discountMultiplier = 1 - (discountRate / 100);

    return items.map(item => ({
      ...item,
      uniPriceHT: item.uniPriceHT * discountMultiplier
    }));
  }

  // Validate credit limit
  validateCreditLimit(
    client: ClientResponseDto,
    invoiceTotalTTC: number,
    currentOutstanding: number
  ): { hasSufficientCredit: boolean; currentUsage: number; remainingCredit: number; message: string } {
    // No credit limit set
    if (!client.creditLimit || client.creditLimit <= 0) {
      return {
        hasSufficientCredit: true,
        currentUsage: currentOutstanding,
        remainingCredit: Infinity,
        message: 'No credit limit restrictions apply'
      };
    }

    const totalWithOutstanding = currentOutstanding + invoiceTotalTTC;
    const hasSufficientCredit = totalWithOutstanding <= client.creditLimit;
    const remainingCredit = Math.max(0, client.creditLimit - currentOutstanding);

    return {
        hasSufficientCredit,
        currentUsage: currentOutstanding,
        remainingCredit,
        message: hasSufficientCredit
          ? this.t('invoices.responses.errors.HAS_SUFFICIENT_CREDIT', { remainingCredit: remainingCredit.toFixed(2) })
          : this.t('invoices.responses.errors.insufficient_credit', {
        creditLimit: client.creditLimit.toFixed(2),
        currentOutstanding: currentOutstanding.toFixed(2),
        invoiceTotal: invoiceTotalTTC.toFixed(2)
      })
    }
  }

  // invoice.service.ts
  async validateInvoiceBeforeSubmission(
    client: any,
    items: CreateInvoiceDto['items']
  ): Promise<InvoiceValidationResult> {
    const errors: string[] = [];
    const warnings: string[] = [];

    // Only UI-level guards — backend will re-validate everything securely
    if (client.isBlocked) {
      errors.push(this.t('invoices.responses.errors.CLIENT_BLOCKED', {client: client.name}));
      return { isValid: false, errors, warnings };
    }

    if (client.isDeleted) {
      errors.push(this.t('invoices.responses.errors.CLIENT_DELETED', {client: client.name}));
      return { isValid: false, errors, warnings };
    }

    if (!items || items.length === 0) {
      errors.push(this.t('INVOICES.FORM.NO_ITEMS_YET'));
      return { isValid: false, errors, warnings };
    }

    return { isValid: true, errors, warnings };
  }


  update(id: string, dto: UpdateInvoiceDto): Observable<InvoiceDto> {
    return this.http.put<InvoiceDto>(`${this.baseUrl}/update/${id}`, dto);
  }

  downloadInvoicePdf(invoiceId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${invoiceId}/pdf`, { responseType: 'blob' });
  }

  getInvoicePdfUrl(invoiceId: string): string {
    return `${this.baseUrl}/${invoiceId}/pdf`;
  }


  // ARticle caching
  getArticleById(id: string): Observable<ArticleResponseDto> {
    return this.http.get<ArticleResponseDto>(
      `${this.baseUrl}/cache/articles/${id}`
    );
  }

  getArticlesPaged(pageNumber = 1, pageSize = 10, search = ''): Observable<PagedResultDto<ArticleResponseDto>> {
    var params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    if (search?.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<PagedResultDto<ArticleResponseDto>>(
      `${this.baseUrl}/cache/articles`,
      { params }
    );
  }

  // CLient caching
  getClientById(id: string): Observable<ClientResponseDto> {
    return this.http.get<ClientResponseDto>(
      `${this.baseUrl}/cache/clients/${id}`
    );
  }

  getClientsPaged(pageNumber = 1, pageSize = 10, search= ''): Observable<PagedResultDto<ClientResponseDto>> {
    var params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    if (search?.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResultDto<ClientResponseDto>>(
      `${this.baseUrl}/cache/clients`,
      { params }
    );
  }


}