using ERP.StockService.Application.DTOs;

namespace ERP.StockService.Domain.LocalCache.Client;

public sealed class CategoryCache
{

    // ── Identity ──────────────────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;

    // ── Business rules ────────────────────────────────────────────────────────
    public int DelaiRetour { get; private set; }
    public int DuePaymentPeriod { get; private set; }
    public decimal? DiscountRate { get; private set; }  // 0.00 – 1.00
    public decimal? CreditLimitMultiplier { get; private set; }  // e.g. 1.5 = 150%
    public bool UseBulkPricing { get; private set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; private set; } = false;

    // ── Audit ─────────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // ── EF Core constructor ───────────────────────────────────────────────────
    private CategoryCache() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static CategoryCache Create(ClientCategoryResponseDto dto)
    {
        return new CategoryCache
        {
            Id = dto.Id,
            TenantId= dto.TenantId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpperInvariant(),
            DelaiRetour = dto.DelaiRetour,
            DuePaymentPeriod = dto.DuePaymentPeriod,
            UseBulkPricing = dto.UseBulkPricing,
            DiscountRate = dto.DiscountRate,
            CreditLimitMultiplier = dto.CreditLimitMultiplier,
            IsDeleted = dto.IsDeleted,
            IsActive = dto.IsActive,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }


    // ── Update ────────────────────────────────────────────────────────────────
    public void Update(
    string name,
    string code,
    int delaiRetour,
    bool isActive,
    bool isDeleted,
    DateTime createdAt,
    int duePaymentPeriod,          // ← int, not int?
    DateTime? updatedAt = null,
    bool useBulkPricing = false,
    decimal? discountRate = null,
    decimal? creditLimitMultiplier = null)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        DelaiRetour = delaiRetour;
        DuePaymentPeriod = duePaymentPeriod;
        UseBulkPricing = useBulkPricing;
        DiscountRate = discountRate;
        CreditLimitMultiplier = creditLimitMultiplier;
        CreatedAt = createdAt;
        UpdatedAt = DateTime.UtcNow;
        IsDeleted = isDeleted;
        IsActive = isActive;
    }

    public void Delete()
    {
        GuardNotDeleted();
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
    private void GuardNotDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted category.");
    }
}