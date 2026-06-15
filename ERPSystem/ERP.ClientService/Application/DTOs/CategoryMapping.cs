using ERP.ClientService.Domain;

namespace ERP.ClientService.Application.DTOs;

public static class CategoryMappings
{
    public static CategoryResponseDto ToResponseDto(this Category category) =>
        new(
            Id: category.Id,
            Name: category.Name,
            Code: category.Code,
            DelaiRetour: category.DelaiRetour,
            DuePaymentPeriod: category.DuePaymentPeriod,
            DiscountRate: category.DiscountRate,
            CreditLimitMultiplier: category.CreditLimitMultiplier,
            UseBulkPricing: category.UseBulkPricing,
            IsActive: category.IsActive,
            IsDeleted: category.IsDeleted,
            CreatedAt: category.CreatedAt,
            UpdatedAt: category.UpdatedAt,
            TenantId: category.TenantId
        );
}