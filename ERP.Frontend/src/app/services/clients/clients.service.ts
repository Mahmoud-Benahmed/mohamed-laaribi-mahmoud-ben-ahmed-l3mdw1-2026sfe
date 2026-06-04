import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { ClientCategoryResponseDto } from './categories.service';

// ── DTOs ─────────────────────────────────────────────
export interface AssignedCategoryDto {
  id: string;
  name: string;
  code: string;
  delaiRetour: number;
  duePaymentPeriod: number;
  discountRate: number | null;
  creditLimitMultiplier: number | null;
  useBulkPricing: boolean;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string,
  updatedAt?: string
}
export interface ClientResponseDto {
  id: string;
  name: string;
  email: string;
  address: string;
  duePaymentPeriod: number;
  canUseBulkPricing: boolean;
  phone: string | null;
  taxNumber: string | null;
  creditLimit: number|null;
  effectiveCreditLimit: number|null;
  delaiRetour: number|null;
  effectiveDelaiRetour: number|null;
  isBlocked: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string;
  categories: AssignedCategoryDto[];
}

export interface CategoryClientCountDto {
  categoryId: string;
  categoryName: string;
  clientCount: number;
}

export interface ClientStatsDto {
  totalClients: number;
  activeClients: number;
  blockedClients: number;
  deletedClients: number;
  clientsPerCategory: CategoryClientCountDto[];
}

export interface CreateClientRequestDto {
  name: string;
  email: string;
  address: string;
  duePaymentPeriod: number | null;
  phone: string | null;
  taxNumber: string | null;
  creditLimit: number | null;
  delaiRetour: number | null;
}

export interface UpdateClientRequestDto extends CreateClientRequestDto {}

export interface AddCategoryRequestDto {
  categoryId: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
}

// ── SERVICE ──────────────────────────────────────────

@Injectable({
  providedIn: 'root'
})
export class ClientsService {
  private readonly baseUrl = `${environment.apiUrl}/clients`;

  private readonly endpoints = {
    deleted: `${this.baseUrl}/deleted`,
    byCategory: `${this.baseUrl}/by-category`,
    byName: `${this.baseUrl}/by-name`,
    stats: `${this.baseUrl}/stats`,
    restore: (id: string) => `${this.baseUrl}/restore/${id}`,
    block: (id: string) => `${this.baseUrl}/block/${id}`,
    unblock: (id: string) => `${this.baseUrl}/unblock/${id}`,
    creditLimit: (id: string) => `${this.baseUrl}/${id}/credit-limit`,
    returnWindow: (id: string) => `${this.baseUrl}/${id}/return-window`,
    effectiveReturn: (id: string) => `${this.baseUrl}/${id}/return-window/effective`,
    canPlaceOrder: (id: string) => `${this.baseUrl}/${id}/can-place-order`,
    categories: (id: string) => `${this.baseUrl}/${id}/categories`
  };

  constructor(private readonly http: HttpClient) {}

  // ── PARAM BUILDER ──────────────────────────────────

  private buildParams<T extends Record<string, any>>(paramsObj: T): HttpParams {
    let params = new HttpParams();

    Object.keys(paramsObj).forEach(key => {
      const value = paramsObj[key];
      if (value !== undefined && value !== null) {
        params = params.set(key, value.toString());
      }
    });

    return params;
  }

  // ── GET ────────────────────────────────────────────

  getAll(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientResponseDto>> {
    const params = this.buildParams({ pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientResponseDto>>(this.baseUrl, { params });
  }

  getDeleted(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientResponseDto>> {
    const params = this.buildParams({ pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientResponseDto>>(this.endpoints.deleted, { params });
  }

  getById(id: string): Observable<ClientResponseDto> {
    return this.http.get<ClientResponseDto>(`${this.baseUrl}/${id}`);
  }

  getByCategory(categoryId: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientResponseDto>> {
    const params = this.buildParams({ categoryId, pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientResponseDto>>(this.endpoints.byCategory, { params });
  }

  getByName(nameFilter: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientResponseDto>> {
    const params = this.buildParams({ nameFilter, pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientResponseDto>>(this.endpoints.byName, { params });
  }

  getStats(): Observable<ClientStatsDto> {
    return this.http.get<ClientStatsDto>(this.endpoints.stats);
  }

  // ── CREATE / UPDATE ────────────────────────────────

  create(dto: CreateClientRequestDto): Observable<ClientResponseDto> {
    return this.http.post<ClientResponseDto>(this.baseUrl, dto);
  }

  update(id: string, dto: UpdateClientRequestDto): Observable<ClientResponseDto> {
    return this.http.put<ClientResponseDto>(`${this.baseUrl}/${id}`, dto);
  }

  // ── DELETE / RESTORE ───────────────────────────────

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  restore(id: string): Observable<void> {
    return this.http.patch<void>(this.endpoints.restore(id), {});
  }

  // ── BLOCK / UNBLOCK ────────────────────────────────

  block(id: string): Observable<ClientResponseDto> {
    return this.http.patch<ClientResponseDto>(this.endpoints.block(id), {});
  }

  unblock(id: string): Observable<ClientResponseDto> {
    return this.http.patch<ClientResponseDto>(this.endpoints.unblock(id), {});
  }

  toggleBlock(client: ClientResponseDto): Observable<ClientResponseDto> {
    return client.isBlocked ? this.unblock(client.id) : this.block(client.id);
  }

  // ── CREDIT LIMIT ───────────────────────────────────

  setCreditLimit(id: string, limit: number): Observable<ClientResponseDto> {
    return this.http.put<ClientResponseDto>(this.endpoints.creditLimit(id), { limit });
  }

  removeCreditLimit(id: string): Observable<ClientResponseDto> {
    return this.http.delete<ClientResponseDto>(this.endpoints.creditLimit(id));
  }

  // ── DELAI RETOUR ───────────────────────────────────

  setDelaiRetour(id: string, days: number): Observable<ClientResponseDto> {
    return this.http.put<ClientResponseDto>(this.endpoints.returnWindow(id), { days });
  }

  clearDelaiRetour(id: string): Observable<ClientResponseDto> {
    return this.http.delete<ClientResponseDto>(this.endpoints.returnWindow(id));
  }

  getEffectiveDelaiRetour(id: string): Observable<{ effectiveDays: number | null }> {
    return this.http.get<{ effectiveDays: number | null }>(
      this.endpoints.effectiveReturn(id)
    );
  }

  // ── BUSINESS LOGIC ─────────────────────────────────

  canPlaceOrder(
    id: string,
    orderAmount: number,
    currentBalance: number
  ): Observable<{ canPlace: boolean }> {
    const params = this.buildParams({ orderAmount, currentBalance });
    return this.http.get<{ canPlace: boolean }>(
      this.endpoints.canPlaceOrder(id),
      { params }
    );
  }

  // ── CATEGORY MANAGEMENT ────────────────────────────

  addCategory(id: string, dto: AddCategoryRequestDto): Observable<ClientResponseDto> {
    return this.http.post<ClientResponseDto>(this.endpoints.categories(id), dto);
  }

  removeCategory(id: string, categoryId: string): Observable<ClientResponseDto> {
    return this.http.delete<ClientResponseDto>(
      `${this.endpoints.categories(id)}/${categoryId}`
    );
  }

  // ── HELPERS (UI LOGIC) ─────────────────────────────

  getCategoryNames(client: ClientResponseDto): string {
    return client.categories.map(c => c.name).join(', ');
  }

  getPrimaryCategory(client: ClientResponseDto): string {
    return client.categories[0]?.name ?? '';
  }

  hasCategory(client: ClientResponseDto, categoryId: string): boolean {
    return client.categories.some(c => c.id === categoryId);
  }
}
