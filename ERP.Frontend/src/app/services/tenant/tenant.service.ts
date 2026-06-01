import { Injectable, inject, signal } from '@angular/core';
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

@Injectable({ providedIn: 'root' })
export class TenantService {
  private http    = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/tenants`;

  private _settings = signal<TenantSettingsDto | null>(null);

  // ── Public readonly signal (inject TenantService anywhere, read settings()) ─
  readonly settings = this._settings.asReadonly();

  get name():           string        { return this._settings()?.name           ?? ''; }
  get email():          string        { return this._settings()?.email          ?? ''; }
  get phone():          string        { return this._settings()?.phone          ?? ''; }
  get address():        string        { return this._settings()?.address        ?? ''; }
  get slug():           string        { return this._settings()?.slug           ?? ''; }
  get logoUrl():        string | null { return this._settings()?.logoUrl        ?? null; }
  get primaryColor():   string        { return this._settings()?.primaryColor   ?? ''; }
  get secondaryColor(): string        { return this._settings()?.secondaryColor ?? ''; }
  get currency():       string        { return this._settings()?.currency       ?? ''; }
  get locale():         string        { return this._settings()?.locale         ?? ''; }
  get timezone():       string        { return this._settings()?.timezone       ?? ''; }

  loadTenantSettings(id: string): Observable<TenantSettingsDto | null> {
    if (this._settings() !== null) {
      return of(this._settings());           // already cached → skip the HTTP call
    }
    return this.getTenantSettings(id).pipe(
      tap(dto => this._settings.set(dto)),
      catchError(err => {
        console.error('Failed to load tenant settings:', err);
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