// ─── DTOs ────────────────────────────────────────────────────────────────────

export interface AuthUserGetResponseDto {
  id: string;
  email: string;
  login: string;
  fullName: string;
  roleId: string;
  roleName: string;
  mustChangePassword: boolean;
  settings: UserSettings
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  lastLoginAt: string | null;
}
export type ThemeType= 'light' | 'dark';
export type LanguageType= 'fr' | 'en';
export interface UserSettings{
  theme: ThemeType,
  language: LanguageType
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface AuthResponseDto {
  accessToken: string;
  refreshToken: string;
  mustChangePassword: boolean;
  expiresAt: string;
  tenantSlug: string | null;
}

export interface LoginRequestDto {
  login: string;
  password: string;
}

export interface RegisterRequestDto {
  login: string;
  email: string;
  fullName: string;
  password: string;
  roleId: string | null;
}

export interface ChangeProfilePasswordRequestDto {
  currentPassword: string;
  newPassword: string;
}

export interface AdminChangeProfileRequest {
  newPassword: string;
}

export interface RefreshTokenRequestDto {
  refreshToken: string;
}

export interface UpdateProfileDto {
  email: string;
  fullName: string;
}

export interface UserStatsDto {
  totalUsers: number;
  activeUsers: number;
  deactivatedUsers: number;
  deletedUsers: number;
}

export interface RoleResponseDto {
  id: string;
  libelle: string;
}

export interface ControleResponseDto {
  id: string;
  category: string;
  libelle: string;
  description: string;
}

export interface PrivilegeResponseDto {
  id: string;
  roleId: string;
  controleId: string;
  controleLibelle: string;
  controleCategory: string;
  isGranted: boolean;
}
