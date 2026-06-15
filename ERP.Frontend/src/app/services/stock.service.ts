import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { environment } from '../environment';
import { ArticleResponseDto, PagedResultDto } from './articles/articles.service';
import { ClientResponseDto } from './clients/clients.service';
import { FournisseurResponse } from './fournisseur.service';

// ── Enums ─────────────────────────────────────────────────────────────────────
export enum RetourSourceType {
  BonEntre  = 'BonEntre',
  BonSortie = 'BonSortie',
}

// ── Shared ────────────────────────────────────────────────────────────────────
export interface PagedResult<T> {
  items:      T[];
  totalCount: number;
  pageNumber: number;
  pageSize:   number;
  totalPages: number;
}

// ── Lignes ────────────────────────────────────────────────────────────────────
export interface LigneResponseDto{
  id: string,
  articleId: string,
  quantity: number;
  price: number;
  remarque: string | null;
  total: number;
}
export interface LigneRequestDto{
  articleId: string,
  quantity: number;
  price: number;
  remarque: string | null;
}

// ── BonEntre ──────────────────────────────────────────────────────────────────
export interface BonEntreResponse {
  id:              string;
  fournisseurId:   string;
  numero:          string;
  observation?:     string;
  createdAt:       string;
  updatedAt:       string | null;
  lignes:          LigneResponseDto[];
  total:           number;
}

export interface CreateBonEntreRequest {
  fournisseurId: string;
  observation?:  string | null;
  numero:        string;
  lignes?:       LigneRequestDto[] | null;
}

export interface UpdateBonEntreRequest {
  fournisseurId:string;
  observation?: string | null;
  lignes?:       LigneRequestDto[] | null;
}

// ── BonSortie ─────────────────────────────────────────────────────────────────
export interface BonSortieResponse {
  id:          string;
  clientId:    string;
  numero:      string;
  observation: string | null;
  createdAt:   string;
  updatedAt:   string | null;
  lignes:      LigneResponseDto[];
  total:       number;
}

export interface CreateBonSortieRequest {
  clientId:     string;
  observation?: string | null;
  lignes?:      LigneRequestDto[] | null;
}

export interface UpdateBonSortieRequest {
  clientId:     string;
  observation?: string | null;
}

// ── BonRetour ─────────────────────────────────────────────────────────────────
export interface BonRetourResponse {
  id:          string;
  sourceId:    string;
  sourceType:  RetourSourceType;
  numero:      string;
  motif:       string;
  observation: string | null;
  createdAt:   string;
  updatedAt:   string | null;
  lignes:      LigneResponseDto[];
  total:       number;
}

export interface BonStatsDto {
  totalCount:  number;
}

export interface CreateBonRetourRequest {
  sourceId:     string;
  sourceType:   RetourSourceType;
  motif:        string;
  observation?: string | null;
  lignes?:      LigneRequestDto[] | null;
}

export interface UpdateBonRetourRequest {
  sourceId:     string;
  motif:        string;
  observation?: string | null;
  lignes?:      LigneRequestDto[] | null;
}

export type BonRecord = BonEntreResponse | BonSortieResponse | BonRetourResponse;

export interface StockItem extends ArticleResponseDto {
  quantity: number;
}

export interface StockStatusResponse {
  inStock: StockItem[];  // or IN_STOCK if your API returns uppercase
  outStock: StockItem[]; // or OUT_STOCK
}

// If your API returns uppercase property names
export interface StockStatusResponseUppercase {
  IN_STOCK: StockItem[];
  OUT_STOCK: StockItem[];
}

// ─────────────────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class StockService {
  private readonly base = `${environment.routes.stock}`;

  constructor(private http: HttpClient) {}

  // ── Helpers ─────────────────────────────────────────────────────────────────
  private pagedParams(page: number, size: number): HttpParams {
    return new HttpParams().set('page', page).set('size', size);
  }

  // ===========================================================================
  // BON ENTRES
  // ===========================================================================

  getBonEntres(page = 1, size = 10): Observable<PagedResult<BonEntreResponse>> {
    return this.http.get<PagedResult<BonEntreResponse>>(
      `${this.base}/bon-entres`,
      { params: this.pagedParams(page, size) }
    );
  }

  getBonEntreById(id: string): Observable<BonEntreResponse> {
    return this.http.get<BonEntreResponse>(`${this.base}/bon-entres/${id}`);
  }

  getBonEntresByFournisseur(fournisseurId: string, page = 1, size = 10): Observable<PagedResult<BonEntreResponse>> {
    return this.http.get<PagedResult<BonEntreResponse>>(
      `${this.base}/bon-entres/by-fournisseur/${fournisseurId}`,
      { params: this.pagedParams(page, size) }
    );
  }

  getBonEntresByDateRange(
    from: Date,
    to: Date,
    page = 1,
    size = 10
  ): Observable<PagedResult<BonEntreResponse>> {

    const params = this.pagedParams(page, size)
      .set('from', from.toISOString().split('T')[0])
      .set('to', to.toISOString().split('T')[0]);

    return this.http.get<PagedResult<BonEntreResponse>>(
      `${this.base}/bon-entres/by-date-range`,
      { params }
    );
  }

  createBonEntre(dto: CreateBonEntreRequest): Observable<BonEntreResponse> {
    return this.http.post<BonEntreResponse>(`${this.base}/bon-entres`, dto);
  }

  updateBonEntre(id: string, dto: UpdateBonEntreRequest): Observable<BonEntreResponse> {
    return this.http.put<BonEntreResponse>(`${this.base}/bon-entres/${id}`, dto);
  }

  deleteBonEntre(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/bon-entres/${id}`);
  }
  // ===========================================================================
  // BON SORTIES
  // ===========================================================================

  getBonSorties(page = 1, size = 10): Observable<PagedResult<BonSortieResponse>> {
    return this.http.get<PagedResult<BonSortieResponse>>(
      `${this.base}/bon-sorties`,
      { params: this.pagedParams(page, size) }
    );
  }

  getBonSortieById(id: string): Observable<BonSortieResponse> {
    return this.http.get<BonSortieResponse>(`${this.base}/bon-sorties/${id}`);
  }

  getBonSortiesByClient(clientId: string, page = 1, size = 10): Observable<PagedResult<BonSortieResponse>> {
    return this.http.get<PagedResult<BonSortieResponse>>(
      `${this.base}/bon-sorties/by-client/${clientId}`,
      { params: this.pagedParams(page, size) }
    );
  }

  getBonSortiesByDateRange(from: Date, to: Date, page = 1, size = 10): Observable<PagedResult<BonSortieResponse>> {
    const params = this.pagedParams(page, size)
      .set('from', from.toISOString().split('T')[0])
      .set('to', to.toISOString().split('T')[0]);
    return this.http.get<PagedResult<BonSortieResponse>>(
      `${this.base}/bon-sorties/by-date-range`,
      { params }
    );
  }

  createBonSortie(dto: CreateBonSortieRequest): Observable<BonSortieResponse> {
    return this.http.post<BonSortieResponse>(`${this.base}/bon-sorties`, dto);
  }

  updateBonSortie(id: string, dto: UpdateBonSortieRequest): Observable<BonSortieResponse> {
    return this.http.put<BonSortieResponse>(`${this.base}/bon-sorties/${id}`, dto);
  }

  deleteBonSortie(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/bon-sorties/${id}`);
  }

  // ===========================================================================
  // BON RETOURS
  // ===========================================================================

  getBonRetours(page = 1, size = 10): Observable<PagedResult<BonRetourResponse>> {
    return this.http.get<PagedResult<BonRetourResponse>>(
      `${this.base}/bon-retours`,
      { params: this.pagedParams(page, size) }
    );
  }

  getBonRetourById(id: string): Observable<BonRetourResponse> {
    return this.http.get<BonRetourResponse>(`${this.base}/bon-retours/${id}`);
  }

  getBonRetoursBySource(sourceId: string, page = 1, size = 10): Observable<PagedResult<BonRetourResponse>> {
    return this.http.get<PagedResult<BonRetourResponse>>(
      `${this.base}/bon-retours/by-source/${sourceId}`,
      { params: this.pagedParams(page, size) }
    );
  }

  getBonRetoursByDateRange(from: Date, to: Date, page = 1, size = 10): Observable<PagedResult<BonRetourResponse>> {
    const params = this.pagedParams(page, size)
      .set('from', from.toISOString().split('T')[0])
      .set('to', to.toISOString().split('T')[0]);
    return this.http.get<PagedResult<BonRetourResponse>>(
      `${this.base}/bon-retours/by-date-range`,
      { params }
    );
  }

  createBonRetour(dto: CreateBonRetourRequest): Observable<BonRetourResponse> {
    return this.http.post<BonRetourResponse>(`${this.base}/bon-retours`, dto);
  }

  updateBonRetour(id: string, dto: UpdateBonRetourRequest): Observable<BonRetourResponse> {
    return this.http.put<BonRetourResponse>(`${this.base}/bon-retours/${id}`, dto);
  }

  deleteBonRetour(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/bon-retours/${id}`);
  }


  ///////////////////////////
  // BON STATS
  ///////////////////////////

  getBonEntreStats(): Observable<BonStatsDto> {
    return this.http.get<BonStatsDto>(`${this.base}/bon-entres/stats`);
  }

  getBonSortieStats(): Observable<BonStatsDto> {
    return this.http.get<BonStatsDto>(`${this.base}/bon-sorties/stats`);
  }

  getBonRetourStats(): Observable<BonStatsDto> {
    return this.http.get<BonStatsDto>(`${this.base}/bon-retours/stats`);
  }

  // Quantity
  getArticleCurrentStock(articleId: string): Observable<{ articleId: string; currentStock: number }> {
    return this.http.get<{ articleId: string; currentStock: number }>(
      `${this.base}/quantity/${articleId}`
    );
  }

  getStockArticles(): Observable<StockStatusResponse> {
    return this.http.get<StockStatusResponseUppercase>(`${this.base}/articles`)
      .pipe(
        map(response => ({
          inStock: response.IN_STOCK || [],
          outStock: response.OUT_STOCK || []
        }))
      );
  }



    // ARticle caching
    getArticleById(id: string): Observable<ArticleResponseDto> {
      return this.http.get<ArticleResponseDto>(
        `${this.base}/cache/articles/${id}`
      );
    }

    getArticleByBarcode(barcode: string): Observable<ArticleResponseDto> {
      const params = new HttpParams().set('barcode', barcode);

      return this.http.get<ArticleResponseDto>(
        `${this.base}/cache/articles/by-barcode`,
        { params }
      );
    }

    getArticleByRefCode(refcode: string): Observable<ArticleResponseDto> {
      const params = new HttpParams().set('refcode', refcode);

      return this.http.get<ArticleResponseDto>(
        `${this.base}/cache/articles/by-refcode`,
        { params }
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
        `${this.base}/cache/articles`,
        { params }
      );
    }

    // CLient caching
    getClientById(id: string): Observable<ClientResponseDto> {
      return this.http.get<ClientResponseDto>(
        `${this.base}/cache/clients/${id}`
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
        `${this.base}/cache/clients`,
        { params }
      );
    }

    // FOURNISSEUR caching
    getFournisseurById(id: string): Observable<FournisseurResponse> {
      return this.http.get<FournisseurResponse>(
        `${this.base}/cache/fournisseurs/${id}`
      );
    }

    getFournisseursPaged(pageNumber = 1, pageSize = 10, search= ''): Observable<PagedResultDto<FournisseurResponse>> {
      var params = new HttpParams()
        .set('pageNumber', pageNumber)
        .set('pageSize', pageSize);
      if (search?.trim()) {
        params = params.set('search', search.trim());
      }
      return this.http.get<PagedResultDto<FournisseurResponse>>(
        `${this.base}/cache/fournisseurs`,
        { params }
      );
    }
}
