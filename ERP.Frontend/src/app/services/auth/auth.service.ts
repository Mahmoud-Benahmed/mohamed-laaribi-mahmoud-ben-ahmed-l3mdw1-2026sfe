import { jwtDecode } from 'jwt-decode';
import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { Router } from "@angular/router";
import { environment } from "../../environment";
import { AdminChangeProfileRequest, AuthResponseDto, AuthUserGetResponseDto, ChangeProfilePasswordRequestDto,
        LanguageType, LoginRequestDto, PagedResultDto, RefreshTokenRequestDto, RegisterRequestDto,
        ThemeType, UpdateProfileDto, UserStatsDto
} from "../../interfaces/AuthDto";
import { BehaviorSubject, catchError, Observable, Subject, take, tap, throwError } from 'rxjs';

interface JwtPayload {
  sub: string;
  tenantId: string;
  slug: string;
  login: string;
  role: string;
  theme: 'light' | 'dark';
  language: 'fr' | 'en';
  privilege: string | string[];
  exp: number;
}

export interface UserSettings{
  theme: 'light' | 'dark';
  language: 'fr' | 'en';
}

export const ROLES={
  SYSTEM_ADMIN : "SYSTEM_ADMIN",
  SALES_MANAGER: "SALES_MANAGER",
  STOCK_MANAGER: "STOCK_MANAGER",
  ACCOUNTANT   : "ACCOUNTANT"
}

export const PRIVILEGES = {
  USERS: {
    VIEW_USERS: "VIEW_USERS",
    CREATE_USER: "CREATE_USER",
    UPDATE_USER: "UPDATE_USER",
    DELETE_USER: "DELETE_USER",
    RESTORE_USER: "RESTORE_USER",
    ACTIVATE_USER: "ACTIVATE_USER",
    DEACTIVATE_USER: "DEACTIVATE_USER",
    MANAGE_USERS: "MANAGE_USERS",
    ASSIGN_ROLES: "ASSIGN_ROLES",
    CREATE_CONTROLE:"CREATE_CONTROLE",
    UPDATE_CONTROLE:"UPDATE_CONTROLE",
    DELETE_CONTROLE:"DELETE_CONTROLE",
    CREATE_ROLE:"CREATE_ROLE",
    UPDATE_ROLE:"UPDATE_ROLE",
    DELETE_ROLE:"DELETE_ROLE",
    EDIT_SYSTEM_SETTINGS: "EDIT_SYSTEM_SETTINGS"
  },
  AUDIT: {
    MANAGE_AUDITLOGS: "MANAGE_AUDITLOGS",
  },
  CLIENTS: {
    VIEW_CLIENTS: "VIEW_CLIENTS",
    CREATE_CLIENT: "CREATE_CLIENT",
    UPDATE_CLIENT: "UPDATE_CLIENT",
    DELETE_CLIENT: "DELETE_CLIENT",
    RESTORE_CLIENT: "RESTORE_CLIENT",
    CREATE_CLIENT_CATEGORIES: "CREATE_CLIENT_CATEGORIES",
    UPDATE_CLIENT_CATEGORIES: "UPDATE_CLIENT_CATEGORIES",
    DELETE_CLIENT_CATEGORIES: "DELETE_CLIENT_CATEGORIES",
    RESTORE_CLIENT_CATEGORIES: "RESTORE_CLIENT_CATEGORIES",
    MANAGE_CLIENTS: "MANAGE_CLIENTS",
  },
  ARTICLES: {
    VIEW_ARTICLES: "VIEW_ARTICLES",
    CREATE_ARTICLE: "CREATE_ARTICLE",
    UPDATE_ARTICLE: "UPDATE_ARTICLE",
    DELETE_ARTICLE: "DELETE_ARTICLE",
    RESTORE_ARTICLE: "RESTORE_ARTICLE",
    CREATE_ARTICLE_CATEGORIES: "CREATE_ARTICLE_CATEGORIES",
    UPDATE_ARTICLE_CATEGORIES: "UPDATE_ARTICLE_CATEGORIES",
    DELETE_ARTICLE_CATEGORIES: "DELETE_ARTICLE_CATEGORIES",
    RESTORE_ARTICLE_CATEGORIES: "RESTORE_ARTICLE_CATEGORIES",
    MANAGE_ARTICLES: "MANAGE_ARTICLES",
  },
  INVOICES: {
    VIEW_INVOICES: "VIEW_INVOICES",
    CREATE_INVOICE: "CREATE_INVOICE",
    UPDATE_DRAFT_INVOICE: "UPDATE_DRAFT_INVOICE",
    DELETE_INVOICE: "DELETE_INVOICE",
    MARK_INVOICE_PAID: "MARK_INVOICE_PAID",
    CANCEL_INVOICE: "CANCEL_INVOICE",
    RESTORE_INVOICE: "RESTORE_INVOICE",
    MANAGE_INVOICES: "MANAGE_INVOICES",
  },
  PAYMENTS: {
    MANAGE_PAYMENTS: "MANAGE_PAYMENTS",
    VIEW_PAYMENTS: "VIEW_PAYMENTS",
    RECORD_PAYMENT: "RECORD_PAYMENT",
    CANCEL_PAYMENT: "CANCEL_PAYMENT",
  },
  STOCK: {
    VIEW_STOCK: "VIEW_STOCK",
    UPDATE_STOCK: "UPDATE_STOCK",
    ADD_ENTRY: "ADD_ENTRY",
    MANAGE_STOCK: "MANAGE_STOCK",
  },
  FOURNISSEURS:{
    VIEW_FOURNISSEURS: "VIEW_FOURNISSEURS",
    CREATE_FOURNISSEUR: "CREATE_FOURNISSEUR",
    UPDATE_FOURNISSEUR: "UPDATE_FOURNISSEUR",
    DELETE_FOURNISSEUR: "DELETE_FOURNISSEUR",
    RESTORE_FOURNISSEUR: "RESTORE_FOURNISSEUR",
    BLOCK_FOURNISSEUR: "BLOCK_FOURNISSEUR",
    UNBLOCK_FOURNISSEUR: "UNBLOCK_FOURNISSEUR",
  },
  REPORTS: {
    VIEW_REPORTS: "VIEW_REPORTS",
    EXPORT_REPORTS: "EXPORT_REPORTS",
  },
  TENANTS:{
    VIEW_TENANTS: "VIEW_TENANTS",
    CREATE_TENANT: "CREATE_TENANT",
    UPDATE_TENANT: "UPDATE_TENANT",
    DELETE_TENANT: "DELETE_TENANT",
    RESTORE_TENANT: "RESTORE_TENANT",
    SUSPEND_TENANT: "SUSPEND_TENANT",
    ACTIVATE_TENANT: "ACTIVATE_TENANT",
    MANAGE_SUBSCRIPTIONS: "MANAGE_SUBSCRIPTIONS",
    VIEW_BILLING: "VIEW_BILLING"
  }
};

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly ACCESS_TOKEN_KEY = 'accessToken';
  private readonly REFRESH_TOKEN_KEY = 'refreshToken';
  private readonly PROFILE_KEY = 'userProfile';
  private _cachedPayload: JwtPayload | null = null;
  private _cachedToken: string | null = null;
  private _isRefreshing = false;

  get isRefreshing(): boolean { return this._isRefreshing; }

  private _userProfile$ = new BehaviorSubject<AuthUserGetResponseDto | null>(
    this.loadProfileFromStorage()
  );
  readonly userProfile$ = this._userProfile$.asObservable();

  private _loggingOut = false;

  private _onLogout$ = new Subject<void>();
  readonly onLogout$ = this._onLogout$.asObservable();

  private readonly baseUrl = `${environment.routes.auth}`;
  private readonly loginUrl = `/login`;

  constructor(private http: HttpClient,   private router: Router) {}

  // =========================
  // TOKEN STORAGE
  // =========================
  storeTokens(response: AuthResponseDto): void {
    localStorage.setItem(this.ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem('expiresAt', response.expiresAt);
    localStorage.setItem('mustChangePassword', String(response.mustChangePassword));
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.ACCESS_TOKEN_KEY);}

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);}

  getExpiresAt(): Date | null {
    const value = localStorage.getItem('expiresAt');
    return value ? new Date(value) : null;}

  getMustChangePassword(): boolean {
    return localStorage.getItem('mustChangePassword') === 'true';}


  clearSession(): void {
    localStorage.removeItem(this.ACCESS_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem('expiresAt');
    localStorage.removeItem('mustChangePassword');
    localStorage.removeItem(this.PROFILE_KEY);
    this._cachedPayload = null;
    this._cachedToken = null;
  }

  isLoggedIn(): boolean {
    if (this._isRefreshing) return true;  // ← treat as logged in during refresh
    const token = this.getAccessToken();
    if (!token) return false;
    try {
      const decoded = jwtDecode<JwtPayload>(token);
      return decoded.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }


  // =========================
  // CLAIM GETTERS
  // =========================
  get JwtPayload(): JwtPayload | null {
      const token = this.getAccessToken();
      if (!token) return null;
      if (token === this._cachedToken) return this._cachedPayload;
      try {
          this._cachedPayload = jwtDecode<JwtPayload>(token);
          this._cachedToken = token;
          return this._cachedPayload;
      } catch {
          return null;
      }
  }

  get TenantId(): string | null {
    return this.JwtPayload?.tenantId ?? null;
  }

  get Slug(): string | null {
    return this.JwtPayload?.slug ?? null;
  }

  get UserId(): string | null {
    return this.JwtPayload?.sub ?? null;
  }

  get Login(): string | null {
    return this.JwtPayload?.login ?? null;
  }

  get Role(): string | null {
    return this.JwtPayload?.role ?? null;
  }

  get Theme(): ThemeType {
    return this._userProfile$.value?.settings?.theme ?? 'light';
  }

  get Language(): LanguageType {
    return this._userProfile$.value?.settings?.language ?? 'en';
  }
  // =========================
  // PRIVILEGES
  // =========================
  get Privileges(): string[] {
    const payload = this.JwtPayload;
    if (!payload?.privilege) return [];
    return Array.isArray(payload.privilege) ? payload.privilege : [payload.privilege];
  }

  hasPrivilege(...privileges: string[]): boolean {
    if (!privileges || privileges.length === 0) return false;
    return privileges.some(privilege => this.Privileges.includes(privilege));
  }

  storeMustChangePassword(value: boolean): void {
    localStorage.setItem('mustChangePassword', String(value));
  }

  clearMustChangePassword(): void {
    localStorage.removeItem('mustChangePassword');
  }


  // =========================
  // USER PROFILE CACHE
  // =========================
  private loadProfileFromStorage(): AuthUserGetResponseDto | null {
    const raw = localStorage.getItem(this.PROFILE_KEY);
    try {
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }
  get UserProfile(): AuthUserGetResponseDto | null {
    return this._userProfile$.value;
  }

  setUserProfile(profile: AuthUserGetResponseDto): void {
    localStorage.setItem(this.PROFILE_KEY, JSON.stringify(profile));
    this._userProfile$.next(profile);
  }

  clearUserProfile(): void {
    localStorage.removeItem(this.PROFILE_KEY);
    this._userProfile$.next(null);
  }

  // =========================
  // GET ME
  // =========================
  getMe(): Observable<AuthUserGetResponseDto> {
    return this.http.get<AuthUserGetResponseDto>(`${this.baseUrl}/me`);
  }

  // =========================
  // GET BY ID
  // =========================
  getById(id: string): Observable<AuthUserGetResponseDto> {
    return this.http.get<AuthUserGetResponseDto>(`${this.baseUrl}/${id}`);
  }

  // =========================
  // GET BY LOGIN
  // =========================
  getByLogin(login: string): Observable<AuthUserGetResponseDto> {
    return this.http.get<AuthUserGetResponseDto>(`${this.baseUrl}/login/${login}`);
  }

  // ── Auth: User Lists ─────────────────────────────────────────────────────

  /** GET /auth — Get all users (paginated) */
  getUsers(pageNumber: number = 1, pageSize: number = 10): Observable<PagedResultDto<AuthUserGetResponseDto>> {
      const params = new HttpParams().set('pageNumber', pageNumber)
                                      .set('pageSize', pageSize);
      return this.http.get<PagedResultDto<AuthUserGetResponseDto>>(this.baseUrl, { params });
  }

  /** GET /auth/activated — Get all active users (paginated) */
  getActivatedUsers(pageNumber: number = 1,
                    pageSize: number = 10): Observable<PagedResultDto<AuthUserGetResponseDto>> {
      const params = new HttpParams().set('pageNumber', pageNumber)
                                      .set('pageSize', pageSize);
      return this.http.get<PagedResultDto<AuthUserGetResponseDto>>(`${this.baseUrl}/activated`,
                                                                    { params });
  }

  /** GET /auth/deactivated — Get all deactivated users (paginated) */
  getDeactivatedUsers(pageNumber: number = 1,
                      pageSize: number = 10): Observable<PagedResultDto<AuthUserGetResponseDto>> {
    const params = new HttpParams().set('pageNumber', pageNumber)
                                    .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<AuthUserGetResponseDto>>(`${this.baseUrl}/deactivated`,
                                                                  { params });
  }

  /** GET /auth/by-role — Get users filtered by role (paginated) */
  getUsersByRole(roleId: string,
                  pageNumber: number = 1,
                  pageSize: number = 10): Observable<PagedResultDto<AuthUserGetResponseDto>> {
    const params = new HttpParams().set('roleId', roleId)
                                    .set('pageNumber', pageNumber)
                                    .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<AuthUserGetResponseDto>>(`${this.baseUrl}/by-role`,
                                                                  { params });
  }

  /** GET /auth/deleted — Get deleted users (paginated) */
  getDeleted(
    pageNumber: number = 1,
    pageSize: number = 10): Observable<PagedResultDto<AuthUserGetResponseDto>> {
    const params = new HttpParams().set('pageNumber', pageNumber)
                                  .set('pageSize', pageSize);
    return this.http.get<PagedResultDto<AuthUserGetResponseDto>>(`${this.baseUrl}/deleted`,
                                                                  { params });
  }


  // =========================
  // EXISTS BY LOGIN
  // =========================
  existsByLogin(login: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/exists-login/${login}`);
  }

  // =========================
  // EXISTS BY EMAIL
  // =========================
  existsByEmail(email: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/exists-email/${email}`);
  }

  getStats(): Observable<UserStatsDto>{
    return this.http.get<UserStatsDto>(`${this.baseUrl}/stats`);
  }

  // =========================
  // REGISTER
  // =========================
  register(request: RegisterRequestDto): Observable<AuthUserGetResponseDto> {
    return this.http.post<AuthUserGetResponseDto>(`${this.baseUrl}/register`, request);
  }

  // =========================
  // LOGIN
  // =========================
  login(request: LoginRequestDto): Observable<AuthResponseDto> {
    // remove last session's leftovers to prevent access for non logged in user
    this.clearSession();
    this.clearUserProfile();

    return this.http.post<AuthResponseDto>(`${this.loginUrl}`, request).pipe(
      tap(response => this.storeTokens(response))
    );
  }

  // ========================
  // UPDATE
  // ========================

  update(id: string, request: UpdateProfileDto): Observable<AuthUserGetResponseDto>{
    return this.http.put<AuthUserGetResponseDto>(`${this.baseUrl}/update/${id}`, request);
  }

  updateSettings(id: string, settings: UserSettings): Observable<UserSettings>{
    return this.http.put<UserSettings>(`${this.baseUrl}/update/${id}/settings`, settings);
  }
    // ── Auth: Activation ─────────────────────────────────────────────────────

  /** PATCH /auth/{id}/activate — Activate a user account */
  activate(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/activate`, {});
  }

  /** PATCH /auth/{id}/deactivate — Deactivate a user account */
  deactivate(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  /** DELETE /auth/delete/soft/{id} — Soft delete: set IsDeleted to true */
  softDelete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** PATCH /auth/restore/{id} — Recover: reset IsDeleted to false */
  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/restore/${id}`, {});
  }


  // ── Auth: Password Management ────────────────────────────────────────────

  /** PUT /auth/change-password/profile — Change own password (requires current password) */
  changeProfilePassword(payload: ChangeProfilePasswordRequestDto): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/change-password/profile`, payload);
  }

  /** PUT /auth/change-password/{userId} — Admin: force-change a user's password */
  adminChangePassword(userId: string, payload: AdminChangeProfileRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/change-password/${userId}`, payload);
  }

  // =========================
  // REFRESH TOKEN
  // =========================
  refresh(request: RefreshTokenRequestDto): Observable<AuthResponseDto> {
    this._isRefreshing = true;
    return this.http.post<AuthResponseDto>(`${this.baseUrl}/refresh`, request).pipe(
      tap(response => {
        this.storeTokens(response);
        this._isRefreshing = false;
      }),
      catchError(err => {
        this._isRefreshing = false;
        return throwError(() => err);
      })
    );
  }

  // =========================
  // REVOKE + LOGOUT
  // =========================
  revoke(request: RefreshTokenRequestDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/revoke`, request);
  }

  logout(): void {
  if (this._loggingOut || this._isRefreshing) return;
    this._loggingOut = true;

    const refreshToken = this.getRefreshToken();
    this._onLogout$.next();
    if (refreshToken) {
      this.revoke({ refreshToken })
        .pipe(take(1))
        .subscribe({
          complete: () => this.endSession(),
          error: () => this.endSession()
        });
    } else {
      this.endSession();
    }
  }

  public endSession(): void {
    this.clearSession();
    this.clearUserProfile();
    this._loggingOut = false;
    this.router.navigate(['/login']);
  }
}
