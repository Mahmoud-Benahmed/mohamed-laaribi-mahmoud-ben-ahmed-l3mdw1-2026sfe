export interface CreateSubscriptionPlanRequestDto {
  name: string;
  code: string;
  monthlyPrice: number;
  yearlyPrice: number;
  maxUsers: number;
  maxStorageMb: number;
}

export interface UpdateSubscriptionPlanRequestDto {
  name: string;
  code: string;
  monthlyPrice: number;
  yearlyPrice: number;
  maxUsers: number;
  maxStorageMb: number;
}

export interface SubscriptionPlanDto {
  id: string;
  name: string;
  code: string;
  monthlyPrice: number;
  yearlyPrice: number;
  maxUsers: number;
  maxStorageMb: number;
  isActive: boolean;
}

export enum SubscriptionPeriod {
  MONTH="MONTH",
  YEAR="YEAR"
}

export interface AssignSubscriptionRequestDto {
  subscriptionPlanId: string;
  startDate: string;
  period: SubscriptionPeriod;
}

export interface TenantSubscriptionResponseDto {
  tenantId: string;
  startDate: string;
  endDate: string;
  period: SubscriptionPeriod;
  plan: SubscriptionPlanDto | null;
}

export interface CreateTenantRequestDto {
  name: string;
  email: string;
  phone: string;
  subdomainSlug: string;
  address: string;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  currency?: string;
  locale?: string;
  timezone?: string;
  subscription: AssignSubscriptionRequestDto;
}

export interface UpdateTenantRequestDto {
  name: string;
  email: string;
  phone: string;
  subdomainSlug: string;
  address?: string | null;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  currency?: string;
  locale?: string;
  timezone?: string;
}

export interface TenantResponseDto {
  id: string;
  name: string;
  email: string;
  phone: string;
  subdomainSlug: string;
  address: string;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  currency: string;
  locale: string;
  timezone: string;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string;
  subscription?: TenantSubscriptionResponseDto | null;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}