import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environment';
import { CreateTenantRequestDto, SubscriptionPlanDto, TenantResponseDto } from '../interfaces/TenantDto';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly plansUrl = `${environment.apiUrl}/plans`;
  private readonly tenantsUrl = `${environment.apiUrl}/tenants`;

  constructor(private http: HttpClient) {}

  getPlans(): Observable<SubscriptionPlanDto[]> {
    return this.http.get<SubscriptionPlanDto[]>(this.plansUrl);
  }

  createTenant(dto: CreateTenantRequestDto): Observable<TenantResponseDto> {
    return this.http.post<TenantResponseDto>(this.tenantsUrl, dto);
  }
}