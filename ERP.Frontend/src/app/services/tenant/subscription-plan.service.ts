import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environment';
import {
  CreateSubscriptionPlanRequestDto,
  UpdateSubscriptionPlanRequestDto,
  SubscriptionPlanDto,
  PagedResultDto,
} from '../../interfaces/TenantDto'

@Injectable({ providedIn: 'root' })
export class SubscriptionPlanService {
  private http    = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/plans`;

  /** Get all plans (paginated) */
  getAllPlans(page = 1, pageSize = 10): Observable<PagedResultDto<SubscriptionPlanDto>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http
      .get<PagedResultDto<SubscriptionPlanDto>>(this.baseUrl, { params })
      .pipe(catchError(this.handleError));
  }

  /** Get a single plan by ID */
  getPlanById(id: string): Observable<SubscriptionPlanDto> {
    return this.http
      .get<SubscriptionPlanDto>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Create a new subscription plan */
  createPlan(dto: CreateSubscriptionPlanRequestDto): Observable<SubscriptionPlanDto> {
    return this.http
      .post<SubscriptionPlanDto>(this.baseUrl, dto)
      .pipe(catchError(this.handleError));
  }

  /** Update an existing plan */
  updatePlan(id: string, dto: UpdateSubscriptionPlanRequestDto): Observable<SubscriptionPlanDto> {
    return this.http
      .put<SubscriptionPlanDto>(`${this.baseUrl}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Delete a plan (only if no tenants are assigned) */
  deletePlan(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Activate a plan */
  activatePlan(id: string): Observable<void> {
    return this.http
      .patch<void>(`${this.baseUrl}/${id}/activate`, null)
      .pipe(catchError(this.handleError));
  }

  /** Suspend (deactivate) a plan */
  suspendPlan(id: string): Observable<void> {
    return this.http
      .patch<void>(`${this.baseUrl}/${id}/suspend`, null)
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    return throwError(() => error);
  }
}