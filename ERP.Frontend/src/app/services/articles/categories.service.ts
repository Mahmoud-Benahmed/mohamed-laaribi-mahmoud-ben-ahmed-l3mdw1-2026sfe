// category.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface ArticleCategoryStatsDto {
  activeCategories: number;
  deletedCategories: number;
}

export interface CategoryRequestDto {
  name: string;
  tva: number;
}

export interface ArticleCategoryResponseDto {
  id: string;
  name: string;
  tva: number;
  isDeleted: boolean,
  createdAt: string,
  updatedAt?: string
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly base = `${environment.routes.articles}/categories`;

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

  constructor(private http: HttpClient) {}

  // GET /articles/categories
  getAll(): Observable<ArticleCategoryResponseDto[]> {
    return this.http.get<ArticleCategoryResponseDto[]>(this.base);
  }

  // GET /articles/categories/paged?pageNumber=&pageSize=
  getPaged(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleCategoryResponseDto>> {
    const params = this.buildParams({ pageNumber, pageSize });
    return this.http.get<PagedResultDto<ArticleCategoryResponseDto>>(`${this.base}/paged`, { params });
  }

  getDeleted(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleCategoryResponseDto>> {
    const params = this.buildParams({ pageNumber, pageSize });
    return this.http.get<PagedResultDto<ArticleCategoryResponseDto>>(`${this.base}/deleted`, { params });
  }

  // GET /articles/categories/{id}
  getById(id: string): Observable<ArticleCategoryResponseDto> {
    return this.http.get<ArticleCategoryResponseDto>(`${this.base}/${id}`);
  }

  // GET /articles/categories/by-name?name=
  getByName(name: string): Observable<ArticleCategoryResponseDto> {
    const params = this.buildParams({ name });
    return this.http.get<ArticleCategoryResponseDto>(`${this.base}/by-name`, { params });
  }

  // GET /articles/categories/by-date-range?from=&to=&pageNumber=&pageSize=
  getByDateRange(from: Date, to: Date, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleCategoryResponseDto>> {
    const params = this.buildParams({ from, to, pageNumber, pageSize });
    return this.http.get<PagedResultDto<ArticleCategoryResponseDto>>(`${this.base}/by-date-range`, { params });
  }

  // GET /articles/categories/tva/below?tva=
  getBelowTVA(tva: number): Observable<ArticleCategoryResponseDto[]> {
    const params = this.buildParams({ tva });
    return this.http.get<ArticleCategoryResponseDto[]>(`${this.base}/tva/below`, { params });
  }

  // GET /articles/categories/tva/higher?tva=
  getHigherThanTVA(tva: number): Observable<ArticleCategoryResponseDto[]> {
    const params = this.buildParams({ tva });
    return this.http.get<ArticleCategoryResponseDto[]>(`${this.base}/tva/higher`, { params });
  }

  // GET /articles/categories/tva/between?min=&max=
  getBetweenTVA(min: number, max: number): Observable<ArticleCategoryResponseDto[]> {
    const params = this.buildParams({ min, max });
    return this.http.get<ArticleCategoryResponseDto[]>(`${this.base}/tva/between`, { params });
  }

  // POST /articles/categories
  create(dto: CategoryRequestDto): Observable<ArticleCategoryResponseDto> {
    return this.http.post<ArticleCategoryResponseDto>(this.base, dto);
  }

  // PUT /articles/categories/{id}
  update(id: string, dto: CategoryRequestDto): Observable<ArticleCategoryResponseDto> {
    return this.http.put<ArticleCategoryResponseDto>(`${this.base}/${id}`, dto);
  }

  // DELETE /articles/categories/{id}
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  // RESTORE: /articles/categories/restore/{id}
  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/restore/${id}`, {});
  }

  getStats(): Observable<ArticleCategoryStatsDto>{
    return this.http.get<ArticleCategoryStatsDto>(`${this.base}/stats`);
  }
}
