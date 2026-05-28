import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environment';
import {
  CreateTenantRequestDto,
  UpdateTenantRequestDto,
  TenantResponseDto,
  AssignSubscriptionRequestDto,
  TenantSubscriptionResponseDto,
  PagedResultDto
} from '../../interfaces/TenantDto'

@Injectable({ providedIn: 'root' })
export class TenantService {
  private http    = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/tenants`;

  /** Get all active tenants (no pagination) */
  getAllActiveTenants(): Observable<TenantResponseDto[]> {
    return this.http
      .get<TenantResponseDto[]>(`${this.baseUrl}/active`)
      .pipe(catchError(this.handleError));
  }

  /** Get all tenants (paginated) */
  getAllTenants(page = 1, pageSize = 10): Observable<PagedResultDto<TenantResponseDto>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http
      .get<PagedResultDto<TenantResponseDto>>(this.baseUrl, { params })
      .pipe(catchError(this.handleError));
  }

  /** Get soft-deleted tenants (paginated) */
  getDeletedTenants(page = 1, pageSize = 10): Observable<PagedResultDto<TenantResponseDto>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http
      .get<PagedResultDto<TenantResponseDto>>(`${this.baseUrl}/deleted`, { params })
      .pipe(catchError(this.handleError));
  }

  /** Get a tenant by ID */
  getTenantById(id: string): Observable<TenantResponseDto> {
    return this.http
      .get<TenantResponseDto>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Get a tenant by subdomain slug */
  getTenantBySlug(slug: string): Observable<TenantResponseDto> {
    return this.http
      .get<TenantResponseDto>(`${this.baseUrl}/slug/${slug}`)
      .pipe(catchError(this.handleError));
  }

  /** Create a new tenant with an initial subscription */
  createTenant(dto: CreateTenantRequestDto): Observable<TenantResponseDto> {
    return this.http
      .post<TenantResponseDto>(this.baseUrl, dto)
      .pipe(catchError(this.handleError));
  }

  /** Update an existing tenant */
  updateTenant(id: string, dto: UpdateTenantRequestDto): Observable<TenantResponseDto> {
    return this.http
      .put<TenantResponseDto>(`${this.baseUrl}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Soft-delete a tenant */
  deleteTenant(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Restore a soft-deleted tenant */
  restoreTenant(id: string): Observable<void> {
    return this.http
      .patch<void>(`${this.baseUrl}/${id}/restore`, null)
      .pipe(catchError(this.handleError));
  }

  /** Activate a tenant */
  activateTenant(id: string): Observable<void> {
    return this.http
      .patch<void>(`${this.baseUrl}/${id}/activate`, null)
      .pipe(catchError(this.handleError));
  }

  /** Suspend a tenant */
  suspendTenant(id: string): Observable<void> {
    return this.http
      .patch<void>(`${this.baseUrl}/${id}/suspend`, null)
      .pipe(catchError(this.handleError));
  }

  /** Assign or replace a tenant's subscription plan */
  assignSubscription(id: string, dto: AssignSubscriptionRequestDto): Observable<TenantSubscriptionResponseDto> {
    return this.http
      .post<TenantSubscriptionResponseDto>(`${this.baseUrl}/${id}/subscription`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Remove a tenant's subscription */
  removeSubscription(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/${id}/subscription`)
      .pipe(catchError(this.handleError));
  }

  /** Get a tenant's current subscription details */
  getSubscription(id: string): Observable<TenantSubscriptionResponseDto> {
    return this.http
      .get<TenantSubscriptionResponseDto>(`${this.baseUrl}/${id}/subscription`)
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('TenantService error:', error);
    return throwError(() => error);
  }
}