// ─────────────────────────────────────────────────────────────────────────────
// controle.service.ts
// ─────────────────────────────────────────────────────────────────────────────
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environment';

export interface ControleRequestDto {
  category: string;
  libelle: string;
  description: string;
}

export interface ControleResponseDto {
  id: string;
  category: string;
  libelle: string;
  description: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  /** Derived helpers (computed client-side if needed) */
  totalPages?: number;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
}

@Injectable({ providedIn: 'root' })
export class ControleService {
  /** Base URL – override via environment or injection token in your app. */
  private readonly baseUrl = `${environment.routes.controles}`;

  constructor(private readonly http: HttpClient) {}

  // ── READ ───────────────────────────────────────────────────────────────────

  /**
   * Retrieve a paginated list of all controles.
   */
  getAllPaged(
    pageNumber = 1,
    pageSize = 10
  ): Observable<PagedResultDto<ControleResponseDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http
      .get<PagedResultDto<ControleResponseDto>>(`${this.baseUrl}/paged`, { params })
      .pipe(map(this.enrichPaged));
  }

  getAll(): Observable<ControleResponseDto[]>{
    return this.http.get<ControleResponseDto[]>(this.baseUrl);
  }

  /**
   * Retrieve a single controle by GUID.
   */
  getById(id: string): Observable<ControleResponseDto> {
    return this.http.get<ControleResponseDto>(`${this.baseUrl}/${id}`);
  }

  /**
   * Retrieve a paginated list of controles filtered by category (case-insensitive server-side).
   */
  getByCategory(
    category: string,
    pageNumber = 1,
    pageSize = 10
  ): Observable<PagedResultDto<ControleResponseDto>> {
    const params = new HttpParams()
      .set('category', category)
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http
      .get<PagedResultDto<ControleResponseDto>>(`${this.baseUrl}/by-category`, {
        params,
      })
      .pipe(map(this.enrichPaged));
  }

  // ── WRITE ──────────────────────────────────────────────────────────────────

  /**
   * Create a new controle.
   * Requires an authenticated JWT with a valid `sub` claim.
   */
  create(dto: ControleRequestDto): Observable<ControleResponseDto> {
    return this.http.post<ControleResponseDto>(this.baseUrl, dto);
  }

  /**
   * Update an existing controle identified by GUID.
   * Requires an authenticated JWT with a valid `sub` claim.
   */
  update(id: string, dto: ControleRequestDto): Observable<ControleResponseDto> {
    return this.http.put<ControleResponseDto>(`${this.baseUrl}/${id}`, dto);
  }

  /**
   * Delete a controle by GUID.
   * Returns void on HTTP 204 No Content.
   * Requires an authenticated JWT with a valid `sub` claim.
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  /**
   * Enrich a raw paged response with derived pagination helpers.
   */
  private enrichPaged<T>(
    paged: PagedResultDto<T>
  ): PagedResultDto<T> {
    const totalPages = Math.ceil(paged.totalCount / paged.pageSize);
    return {
      ...paged,
      totalPages,
      hasPreviousPage: paged.pageNumber > 1,
      hasNextPage: paged.pageNumber < totalPages,
    };
  }
}
