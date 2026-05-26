using ERP.ClientService.Domain;

namespace ERP.ClientService.Application.DTOs;

public static class ClientMappings
{
    public static ClientResponseDto ToResponseDto(this Client client) =>
        new(
            Id: client.Id,
            Name: client.Name,
            Email: client.Email,
            Address: client.Address,
            DuePaymentPeriod: client.GetEffectiveDuePaymentPeriod(),
            Phone: client.Phone,
            TaxNumber: client.TaxNumber,
            CreditLimit: client.GetEffectiveCreditLimit(),
            DelaiRetour: client.GetEffectiveDelaiRetour(),
            IsBlocked: client.IsBlocked,
            IsDeleted: client.IsDeleted,
            CreatedAt: client.CreatedAt,
            UpdatedAt: client.UpdatedAt,
            TenantId: client.TenantId,
            Categories: (client.ClientCategories ?? new List<ClientCategory>())
                                    .Where(cc => cc.Category != null && cc.Category.IsActive)
                                    .Select(cc => new CategoryResponseDto(
                                        Id: cc.CategoryId,
                                        Name: cc.Category?.Name ?? string.Empty,
                                        Code: cc.Category?.Code ?? string.Empty,
                                        DelaiRetour: cc.Category?.DelaiRetour ?? 0,
                                        DuePaymentPeriod: cc.Category?.DuePaymentPeriod ?? 0,
                                        DiscountRate: cc.Category?.DiscountRate ?? 0,
                                        CreditLimitMultiplier: cc.Category?.CreditLimitMultiplier ?? 0,
                                        UseBulkPricing: cc.Category?.UseBulkPricing ?? false,
                                        IsDeleted: cc.Category?.IsDeleted ?? false,
                                        IsActive: cc.Category?.IsActive ?? false,
                                        CreatedAt: cc.Category?.CreatedAt ?? DateTime.MinValue,
                                        UpdatedAt: cc.Category?.UpdatedAt ?? DateTime.MinValue,
                                        TenantId: cc.Category?.TenantId)
                                    ).ToList()
        );
}