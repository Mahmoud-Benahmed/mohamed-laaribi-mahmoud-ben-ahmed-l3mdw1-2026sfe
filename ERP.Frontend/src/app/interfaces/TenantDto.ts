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

export interface CreateTenantRequestDto {
  name: string;
  email: string;
  phone: string;
  subdomainSlug: string;
  logoUrl?: string;
  primaryColor?: string;
  secondaryColor?: string;
  currency: string;
  locale: string;
  timezone: string;
}

export interface TenantResponseDto {
  id: string;
  name: string;
  email: string;
  phone: string;
  subdomainSlug: string;
  logoUrl?: string;
  primaryColor?: string;
  secondaryColor?: string;
  currency: string;
  locale: string;
  timezone: string;
  isActive: boolean;
  createdAt: string;
}