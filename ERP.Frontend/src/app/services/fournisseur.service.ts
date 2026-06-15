import { inject, Injectable } from '@angular/core';
import { environment } from '../environment';
import { map, Observable } from 'rxjs';
import { HttpClient, HttpParams } from '@angular/common/http';

export interface PagedResult<T> {
  items:      T[];
  totalCount: number;
  pageNumber: number;
  pageSize:   number;
  totalPages: number;
}

export interface FournisseurStatsDto {
  totalFournisseurs:  number;
  activeFournisseurs: number;
  blockedFournisseurs: number;
  deletedFournisseurs: number;
}

// ── Fournisseur ───────────────────────────────────────────────────────────────
export interface FournisseurResponse {
  id:         string;
  name:       string;
  address:    string;
  phone:      string;
  email:      string | null;
  taxNumber:  string;
  rib:        string;
  isDeleted:  boolean;
  isBlocked:  boolean;
  createdAt:  string;
  updatedAt:  string | null;
}

export interface CreateFournisseurRequest {
  name:       string;
  address:    string;
  phone:      string;
  taxNumber:  string | null;
  rib:        string;
  email:     string | null;
}

export interface UpdateFournisseurRequest {
  name:       string;
  address:    string;
  phone:      string;
  taxNumber:  string | null;
  rib:        string;
  email:     string | null;
}


@Injectable({
  providedIn: 'root',
})
export class FournisseurService {
    private http = inject(HttpClient);

    private readonly base = `/fournisseurs`;
    private pagedParams(page: number, size: number): HttpParams {
      return new HttpParams().set('page', page).set('size', size);
    }
    // ===========================================================================
    // FOURNISSEURS
    // ===========================================================================

    getFournisseurs(page = 1, size = 10): Observable<PagedResult<FournisseurResponse>> {
      return this.http.get<PagedResult<FournisseurResponse>>(
        `${this.base}`,
        { params: this.pagedParams(page, size) }
      );
    }

    getBlockedFournisseurs(page = 1, size = 10): Observable<PagedResult<FournisseurResponse>> {
      return this.http.get<PagedResult<FournisseurResponse>>(
        `${this.base}`,
        { params: this.pagedParams(page, size) }
      ).pipe(
        map((res) => ({
          ...res,
          items: res.items.filter((f) => f.isBlocked)
        }))
      );
    }

    getFournisseurById(id: string): Observable<FournisseurResponse> {
      return this.http.get<FournisseurResponse>(`${this.base}/${id}`);
    }

    getDeletedFournisseurs(page = 1, size = 10): Observable<PagedResult<FournisseurResponse>> {
      return this.http.get<PagedResult<FournisseurResponse>>(
        `${this.base}/deleted`,
        { params: this.pagedParams(page, size) }
      );
    }

    getFournisseursByName(name: string, page = 1, size = 10): Observable<PagedResult<FournisseurResponse>> {
      const params = this.pagedParams(page, size).set('name', name);
      return this.http.get<PagedResult<FournisseurResponse>>(
        `${this.base}/by-name`,
        { params }
      );
    }

    getFournisseurStats(): Observable<FournisseurStatsDto> {
      return this.http.get<FournisseurStatsDto>(`${this.base}/stats`);
    }

    createFournisseur(dto: CreateFournisseurRequest): Observable<FournisseurResponse> {
      return this.http.post<FournisseurResponse>(`${this.base}`, dto);
    }

    updateFournisseur(id: string, dto: UpdateFournisseurRequest): Observable<FournisseurResponse> {
      return this.http.put<FournisseurResponse>(`${this.base}/${id}`, dto);
    }

    deleteFournisseur(id: string): Observable<void> {
      return this.http.delete<void>(`${this.base}/${id}`);
    }

    restoreFournisseur(id: string): Observable<void> {
      return this.http.patch<void>(`${this.base}/${id}/restore`, null);
    }

    blockFournisseur(id: string): Observable<FournisseurResponse> {
      return this.http.patch<FournisseurResponse>(`${this.base}/${id}/block`, null);
    }

    unblockFournisseur(id: string): Observable<FournisseurResponse> {
      return this.http.patch<FournisseurResponse>(`${this.base}/${id}/unblock`, null);
    }

    toggleBlock(fournisseur: FournisseurResponse): Observable<FournisseurResponse> {
      return fournisseur.isBlocked ? this.unblockFournisseur(fournisseur.id) : this.blockFournisseur(fournisseur.id);
    }
}
