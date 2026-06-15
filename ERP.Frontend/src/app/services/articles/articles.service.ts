// article.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { ArticleCategoryResponseDto } from './categories.service';

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface ArticleResponseDto {
  id: string;
  category: ArticleCategoryResponseDto;
  codeRef: string;
  barCode: string;
  libelle: string;
  prix: number;
  unit: UnitEnum;
  tva: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ArticleStatsDto {
  totalCount: number;
  activeCount: number;
  deletedCount: number;
  categoriesCount: number;
}

export interface CreateArticleRequestDto {
  libelle: string;
  prix: number;
  unit: UnitEnum;
  categoryId: string;
  barCode: string;
  tva?: number;
}
export interface UpdateArticleRequestDto extends CreateArticleRequestDto
{}

export enum UnitEnum
{
  Piece = 'Piece',
  Gram = 'Gram',
  Kilogram = 'Kilogram',
  Milligram = 'Milligram',
  Ton = 'Ton',
  Milliliter = 'Milliliter',
  Liter = 'Liter',
  CubicMeter = 'CubicMeter',
  Millimeter = 'Millimeter',
  Centimeter = 'Centimeter',
  Meter = 'Meter',
  Kilometer = 'Kilometer',
  Hour = 'Hour',
  Day = 'Day',
}
export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ArticleService {
  private readonly base = `${environment.routes.articles}`;

  constructor(private http: HttpClient) {}

  // GET /articles?pageNumber=&pageSize=
  getAll(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleResponseDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<ArticleResponseDto>>(this.base, { params });
  }

  // GET /articles/deleted?pageNumber=&pageSize=
  getDeleted(pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleResponseDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<ArticleResponseDto>>(`${this.base}/deleted`, { params });
  }

  // GET /articles/{id}
  getById(id: string): Observable<ArticleResponseDto> {
    return this.http.get<ArticleResponseDto>(`${this.base}/${id}`);
  }

  // GET /articles/by-code?code=
  getByCode(code: string): Observable<ArticleResponseDto> {
    const params = new HttpParams().set('code', code);
    return this.http.get<ArticleResponseDto>(`${this.base}/by-code`, { params });
  }

  // GET /articles/by-category?categoryId=&pageNumber=&pageSize=
  getByCategory(categoryId: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleResponseDto>> {
    const params = new HttpParams()
      .set('categoryId', categoryId)
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<ArticleResponseDto>>(`${this.base}/by-category`, { params });
  }

  // GET /articles/by-libelle?libelleFilter=&pageNumber=&pageSize=
  getByLibelle(libelleFilter: string, pageNumber = 1, pageSize = 10): Observable<PagedResultDto<ArticleResponseDto>> {
    const params = new HttpParams()
      .set('libelleFilter', libelleFilter)
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<ArticleResponseDto>>(`${this.base}/by-libelle`, { params });
  }

  // GET /articles/stats
  getStats(): Observable<ArticleStatsDto> {
    return this.http.get<ArticleStatsDto>(`${this.base}/stats`);
  }

  // POST /articles
  create(dto: CreateArticleRequestDto): Observable<ArticleResponseDto> {
    return this.http.post<ArticleResponseDto>(this.base, dto);
  }

  // PUT /articles/{id}
  update(id: string, dto: UpdateArticleRequestDto): Observable<ArticleResponseDto> {
    return this.http.put<ArticleResponseDto>(`${this.base}/${id}`, dto);
  }

  // PATCH /articles/restore/{id}
  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/restore/${id}`, null);
  }

  // DELETE /articles/{id}
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
