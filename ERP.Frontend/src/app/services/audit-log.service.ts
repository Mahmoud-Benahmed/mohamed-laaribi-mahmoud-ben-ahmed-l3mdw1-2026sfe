import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environment';

// ─── DTOs ─────────────────────────────────────────────────────────────────────

export type AuditAction =
  // Auth
  | 'Login'
  | 'Logout'
  | 'TokenRefreshed'
  | 'TokenRevoked'
  | 'TokenValidated'           // ✅ new
  | 'TokenValidationFailed'    // ✅ new

  // Registration
  | 'UserRegistered'

  // Password
  | 'PasswordChanged'
  | 'PasswordChangedByAdmin'

  // Profile
  | 'ProfileUpdated'

  // Account status
  | 'UserActivated'
  | 'UserDeactivated'
  | 'UserDeleted'
  | 'UserRestored'

  // Role
  | 'RoleCreated'
  | 'RoleUpdated'
  | 'RoleDeleted'

  // Controle
  | 'ControleCreated'
  | 'ControleUpdated'
  | 'ControleDeleted'

  | 'Unauthorized'
  | 'UserNotFound'
  | 'UnhandledError';

export interface AuditLogResponseDto {
  id: string;
  action: AuditAction;
  performedBy: string | null;
  targetUserId: string | null;
  success: boolean;
  failureReason: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  metadata: Record<string, string> | null;
  timestamp: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

// ─── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly baseUrl = `/audit`;

  constructor(private http: HttpClient) {}

  /** GET /auth/audit — Get all logs paginated */
  getAll(pageNumber = 1, pageSize = 20): Observable<PagedResultDto<AuditLogResponseDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<AuditLogResponseDto>>(this.baseUrl, { params });
  }

  /** GET /auth/audit/user/{userId} — Get logs by user (performer or target) */
  getByUser(userId: string, pageNumber = 1, pageSize = 20): Observable<PagedResultDto<AuditLogResponseDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<AuditLogResponseDto>>(
      `${this.baseUrl}/user/${userId}`, { params });
  }

  /** GET /auth/audit/count — Get total audit log count */
  count(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/count`);
  }

  /** DELETE /auth/audit — Clear all audit logs (dev only) */
  clear(): Observable<void> {
    return this.http.delete<void>(this.baseUrl);
  }
}
