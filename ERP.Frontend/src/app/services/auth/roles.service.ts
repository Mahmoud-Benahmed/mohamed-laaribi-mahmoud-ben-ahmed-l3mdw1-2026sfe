// ─────────────────────────────────────────────────────────────────────────────
// role.service.ts
// ─────────────────────────────────────────────────────────────────────────────
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environment';

export interface RoleCreateDto {
  libelle: string;
}

export interface RoleUpdateDto {
  libelle: string;
}

export interface RoleResponseDto {
  id: string;
  libelle: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages?: number;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
}

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly baseUrl = `${environment.routes.roles}`;

  constructor(private readonly http: HttpClient) {}

  // ── READ ───────────────────────────────────────────────────────────────────

  /**
   * GET /auth/roles?pageNumber=&pageSize=
   */
  getAllPaged(
    pageNumber = 1,
    pageSize = 10
  ): Observable<PagedResultDto<RoleResponseDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http
      .get<PagedResultDto<RoleResponseDto>>(this.baseUrl, { params })
      .pipe(map(this.enrichPaged));
  }

  getAll(): Observable<RoleResponseDto[]> {
    return this.http.get<RoleResponseDto[]>(this.baseUrl);
  }

  /**
   * GET /auth/roles/:id
   */
  getById(id: string): Observable<RoleResponseDto> {
    return this.http.get<RoleResponseDto>(`${this.baseUrl}/${id}`);
  }

  // ── WRITE ──────────────────────────────────────────────────────────────────

  /**
   * POST /auth/roles
   */
  create(dto: RoleCreateDto): Observable<RoleResponseDto> {
    return this.http.post<RoleResponseDto>(this.baseUrl, dto);
  }

  /**
   * PUT /auth/roles/:id
   */
  update(id: string, dto: RoleUpdateDto): Observable<RoleResponseDto> {
    return this.http.put<RoleResponseDto>(`${this.baseUrl}/${id}`, dto);
  }

  /**
   * DELETE /auth/roles/:id  →  204 No Content
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  private enrichPaged<T>(paged: PagedResultDto<T>): PagedResultDto<T> {
    const totalPages = Math.ceil(paged.totalCount / paged.pageSize);
    return {
      ...paged,
      totalPages,
      hasPreviousPage: paged.pageNumber > 1,
      hasNextPage: paged.pageNumber < totalPages,
    };
  }
}
