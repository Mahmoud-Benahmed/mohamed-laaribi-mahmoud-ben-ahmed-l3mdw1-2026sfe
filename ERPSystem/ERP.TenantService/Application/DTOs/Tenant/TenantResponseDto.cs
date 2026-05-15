using ERP.TenantService.Application.DTOs.TenantSubscription;

namespace ERP.TenantService.Application.DTOs.Tenant;

public record TenantResponseDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string SubdomainSlug,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string Currency,
    string Locale,
    string Timezone,
    bool IsActive,
    DateTime CreatedAt,
    TenantSubscriptionResponseDto? Subscription
);
