using ERP.TenantService.Application.DTOs.TenantSubscription;

namespace ERP.TenantService.Application.DTOs.Tenant;

public record TenantResponseDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Address,
    string SubdomainSlug,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string Currency,
    string Locale,
    string Timezone,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    TenantSubscriptionResponseDto? Subscription
);

public record TenantBrandingDto(
    string Name,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string Currency,
    string Locale,
    string Timezone,
    bool IsActive
);