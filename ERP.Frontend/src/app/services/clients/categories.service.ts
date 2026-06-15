import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';

// ── DTOs ─────────────────────────────────────────────

export interface ClientCategoryResponseDto {
  id: string;
  name: string;
  code: string;
  delaiRetour: number;
  duePaymentPeriod: number;
  discountRate?: number;
  creditLimitMultiplier?: number;
  useBulkPricing: boolean;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateCategoryRequestDto {
  name: string;
  code: string;
  delaiRetour: number;
  duePaymentPeriod: number;
  useBulkPricing?: boolean;
  discountRate?: number | null;
  creditLimitMultiplier?: number | null;
}

export interface UpdateCategoryRequestDto extends CreateCategoryRequestDto {}

export interface CategoryStatsDto {
  totalCategories: number;
  activeCategories: number;
  inactiveCategories: number;
  deletedCategories: number;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber?: number;
  pageSize?: number;
}

// ── SERVICE ──────────────────────────────────────────

@Injectable({
  providedIn: 'root'
})
export class CategoriesService {
  private readonly baseUrl = `${environment.routes.clients}/categories`;

  constructor(private readonly http: HttpClient) {}

  private buildParams(paramsObj: Record<string, any>): HttpParams {
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

  getAll(): Observable<ClientCategoryResponseDto[]> {
    return this.http.get<ClientCategoryResponseDto[]>(`${this.baseUrl}`);
  }

  getAllPaged(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientCategoryResponseDto>> {
    const params = this.buildParams({ pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientCategoryResponseDto>>(`${this.baseUrl}/paged`, { params });
  }

  getDeleted(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientCategoryResponseDto>> {
    const params = this.buildParams({ pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientCategoryResponseDto>>(`${this.baseUrl}/deleted`, { params });
  }

  getById(id: string): Observable<ClientCategoryResponseDto> {
    return this.http.get<ClientCategoryResponseDto>(`${this.baseUrl}/${id}`);
  }

  getPagedByName(nameFilter: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ClientCategoryResponseDto>> {
    const params = this.buildParams({ nameFilter, pageNumber, pageSize });
    return this.http.get<PagedResultDto<ClientCategoryResponseDto>>(`${this.baseUrl}/by-name`, { params });
  }

  getStats(): Observable<CategoryStatsDto> {
    return this.http.get<CategoryStatsDto>(`${this.baseUrl}/stats`);
  }

  // ── CREATE / UPDATE ────────────────────────────────

  create(dto: CreateCategoryRequestDto): Observable<ClientCategoryResponseDto> {
    return this.http.post<ClientCategoryResponseDto>(this.baseUrl, dto);
  }

  update(id: string, dto: UpdateCategoryRequestDto): Observable<ClientCategoryResponseDto> {
    return this.http.put<ClientCategoryResponseDto>(`${this.baseUrl}/${id}`, dto);
  }

  // ── DELETE / RESTORE ───────────────────────────────

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/restore/${id}`, {});
  }

  // ── ACTIVATE / DEACTIVATE ──────────────────────────

  activate(id: string): Observable<ClientCategoryResponseDto> {
    return this.http.patch<ClientCategoryResponseDto>(`${this.baseUrl}/activate/${id}`, {});
  }

  deactivate(id: string): Observable<ClientCategoryResponseDto> {
    return this.http.patch<ClientCategoryResponseDto>(`${this.baseUrl}/deactivate/${id}`, {});
  }
}
