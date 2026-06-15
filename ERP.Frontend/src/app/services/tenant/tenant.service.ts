import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../environment';
import {
  CreateTenantRequestDto,
  UpdateTenantRequestDto,
  TenantResponseDto,
  AssignSubscriptionRequestDto,
  TenantSubscriptionResponseDto,
  PagedResultDto,
  TenantSettingsDto
} from '../../interfaces/TenantDto'
import { AuthService } from '../auth/auth.service';
import { TenantBrandingDto } from '../user-settings.service';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private http    = inject(HttpClient);
  private baseUrl = `${environment.routes.tenants}`;

  private _settings = signal<TenantSettingsDto | null>(null);

  // ── Public readonly signal (inject TenantService anywhere, read settings()) ─
  readonly settings = this._settings.asReadonly();

  readonly name= computed(()=> this._settings()?.name           ?? '');
  readonly email= computed(()=> this._settings()?.email          ?? '');
  readonly phone= computed(()=> this._settings()?.phone          ?? '');
  readonly address= computed(()=> this._settings()?.address        ?? '');
  readonly slug= computed(()=> this._settings()?.slug           ?? '');
  readonly logoUrl= computed(()=> this._settings()?.logoUrl        ?? null);
  readonly primaryColor= computed(()=> this._settings()?.primaryColor   ?? '');
  readonly secondaryColor= computed(()=> this._settings()?.secondaryColor ?? '');
  readonly currency= computed(()=> this._settings()?.currency       ?? '');
  readonly locale= computed(()=> this._settings()?.locale         ?? '');
  readonly timezone= computed(()=> this._settings()?.timezone       ?? '');

  loadTenantSettings(id: string): Observable<TenantSettingsDto | null> {
    if (!id) {
      return of(null);
    }

    if (this._settings() !== null) {
      return of(this._settings());
    }

    return this.getTenantSettings(id).pipe(
      tap(dto => {
        this._settings.set(dto);
      }),
      catchError(err => {
        return of(null);
      })
    );
  }

  patchSettings(partial: Partial<Record<keyof TenantSettingsDto, string | null | undefined>>): void {
    const current = this._settings();
    if (!current) return;

    const sanitized = Object.fromEntries(
      Object.entries(partial).filter(([, v]) => v !== null)
    ) as Partial<TenantSettingsDto>;

    this._settings.set({ ...current, ...sanitized });
  }

  /** Invalidate cache (call after updateTenant so next loadTenantSettings re-fetches) */
  invalidateSettings(): void {
    this._settings.set(null);
  }

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

  getTenantSettings(id: string): Observable<TenantSettingsDto> {
    return this.http
      .get<TenantSettingsDto>(`${this.baseUrl}/admin/${id}`)
      .pipe(catchError(this.handleError));
  }

  getTenantBranding(slug: string): Observable<TenantBrandingDto> {
    return this.http
      .get<TenantBrandingDto>(`${this.baseUrl}/branding/${slug}`)
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
    return throwError(() => error);
  }
}