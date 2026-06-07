import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environment';

// ── DTOs ─────────────────────────────────────────────────────────────────────

export interface PaymentAllocationDto {
  id: string;
  invoiceId: string;
  amountAllocated: number;
}

export interface PaymentDto {
  id: string;
  number: string;
  clientId: string;
  totalAmount: number;
  remainingAmount: number;
  method: string;
  paymentDate: string;
  status: string;
  externalReference?: string;
  notes?: string;
  cancelledAt?: string;
  allocations: PaymentAllocationDto[];
}

export interface PaymentSummaryDto {
  paymentId: string;
  paymentNumber: string;
  amountAllocated: number;
  paymentDate: string;
}

export interface CreateAllocationDto {
  invoiceId: string;
  amountAllocated: number;
}

export interface CreatePaymentDto {
  clientId: string;
  totalAmount: number;
  method: string;
  paymentDate: string;
  externalReference?: string;
  notes?: string;
  allocations: CreateAllocationDto[];
}

export interface CorrectPaymentDto {
  paymentDate: string;
  method: string;
  externalReference?: string;
  notes?: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface PaymentStatsDto{
  done: number;
  cancelled: number;
}

// ── Refund DTOs ───────────────────────────────────────────────────────────────

export interface RefundLineDto {
  paymentId: string;
  paymentAllocationId: string;
  amount: number;
}

export interface RefundRequestDto {
  id: string;
  clientId: string;
  invoiceId: string;           // was missing
  status: string;
  lines: RefundLineDto[];
}

export interface CreateRefundDto {
  clientId: string;
  invoiceId: string;
}

export interface CompleteRefundDto {
  externalReference: string;  // ✅
}

export interface RefundStatsDto{
  totalCount: number;
  completedCount: number;
  pendingCount: number;
}

// ── Invoice Cache DTOs ────────────────────────────────────────────────────────

export interface InvoiceCacheDto {
  id: string;
  clientId: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  status: 'DRAFT' | 'UNPAID' | 'PAID' | 'CANCELLED';
}

export type PaymentStatus = 'DONE' | 'CANCELLED'
// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/payment`;

  // ── Payments ──────────────────────────────────────────────────────────────

  getPaymentById(id: string): Observable<PaymentDto> {
    return this.http.get<PaymentDto>(`${this.base}/${id}`);
  }

  getPaymentByNumber(number: string): Observable<PaymentDto> {
    return this.http.get<PaymentDto>(`${this.base}/number/${number}`);
  }

  getPaymentsByClientId(
    clientId: string,
    pageNumber = 1,
    pageSize = 10
  ): Observable<PagedResultDto<PaymentDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<PaymentDto>>(
      `${this.base}/client/${clientId}`,
      { params }
    );
  }

  getPaymentsPaged(
    pageNumber = 1,
    pageSize = 10,
    status: PaymentStatus= 'DONE',
    search?: string
  ): Observable<PagedResultDto<PaymentDto>> {
    let params = new HttpParams()
      .set('status', status)
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedResultDto<PaymentDto>>(this.base, { params });
  }

  getPaymentsByInvoiceId(invoiceId: string): Observable<PaymentSummaryDto[]> {
    return this.http.get<PaymentSummaryDto[]>(
      `${this.base}/invoice/${invoiceId}`
    );
  }

  createPayment(dto: CreatePaymentDto): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(this.base, dto);
  }

  correctPaymentDetails(
    id: string,
    dto: CorrectPaymentDto
  ): Observable<PaymentDto> {
    return this.http.put<PaymentDto>(`${this.base}/${id}/details`, dto);
  }

  cancelPayment(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/cancel`, null);
  }

  getPaymentStats(): Observable<PaymentStatsDto>{
    return this.http.get<PaymentStatsDto>(`${this.base}/stats`);
  }

  // ── Invoice Cache ─────────────────────────────────────────────────────────

  getInvoicesCached(
    pageNumber = 1,
    pageSize = 10,
    search?: string
  ): Observable<PagedResultDto<InvoiceCacheDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedResultDto<InvoiceCacheDto>>(
      `${this.base}/cache/invoices`,
      { params }
    );
  }

  getInvoiceCacheById(id: string): Observable<InvoiceCacheDto> {
    return this.http.get<InvoiceCacheDto>(
      `${this.base}/cache/invoices/${id}`
    );
  }

  getInvoicesCacheByClient(
    clientId: string,
    pageNumber = 1,
    pageSize = 10
  ): Observable<PagedResultDto<InvoiceCacheDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<InvoiceCacheDto>>(
      `${this.base}/cache/invoices/client/${clientId}`,
      { params }
    );
  }

  getInvoicesCacheByStatus(
    status: InvoiceCacheDto['status'],
    pageNumber = 1,
    pageSize = 10
  ): Observable<PagedResultDto<InvoiceCacheDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<InvoiceCacheDto>>(
      `${this.base}/cache/invoices/status/${status}`,
      { params }
    );
  }

  //===== REFUND ===========================================

  getRefundById(refundId: string): Observable<RefundRequestDto> {
    return this.http.get<RefundRequestDto>(`${this.base}/refunds/${refundId}`);
  }

  getRefundsByClientId(clientId: string): Observable<RefundRequestDto[]> {
    return this.http.get<RefundRequestDto[]>(
      `${this.base}/refunds/client/${clientId}`
    );
  }

  completeRefund(refundId: string, dto: CompleteRefundDto): Observable<void> {
    return this.http.patch<void>(
      `${this.base}/refunds/${refundId}/complete`,
      dto
    );
  }

  getRefundStats(): Observable<RefundStatsDto>{
    return this.http.get<RefundStatsDto>(`${this.base}/refunds/stats`);
  }

}