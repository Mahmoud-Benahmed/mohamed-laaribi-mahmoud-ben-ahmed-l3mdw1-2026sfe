using ERP.InvoiceService.Application.DTOs;

namespace ERP.InvoiceService.Domain.LocalCache.Client;

public class ClientCache
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public string? Phone { get; private set; }
    public int? DuePaymentPeriod { get; private set; }
    public string? TaxNumber { get; private set; }
    public decimal? CreditLimit { get; private set; }  // ← Made nullable
    public int? DelaiRetour { get; private set; }
    public bool IsBlocked { get; private set; } = false;
    public bool IsDeleted { get; private set; } = false;

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Simple List<> with private setter — EF maps this without any Navigation() config
    public List<ClientCategoryCache> ClientCategories { get; private set; } = [];

    private ClientCache() { }

    public static ClientCache Create(ClientResponseDto dto)
    {
        return new ClientCache
        {
            Id = dto.Id,
            TenantId = dto.TenantId,
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Address = dto.Address.Trim(),
            Phone = dto.Phone?.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            CreditLimit = dto.CreditLimit,
            DelaiRetour = dto.DelaiRetour,
            DuePaymentPeriod = dto.DuePaymentPeriod,
            IsBlocked = dto.IsBlocked,
            IsDeleted = dto.IsDeleted,
            CreatedAt = dto.CreatedAt, 
            UpdatedAt = dto.UpdatedAt,
        };
    }

    public void Update(
        string name, string email, string address, decimal? creditLimit = null,
        int? delaiRetour = null, int? duePaymentPeriod = null,
        string? phone = null, string? taxNumber = null,
        bool isBlocked = false, bool isDeleted = false,
        DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        Address = address.Trim();
        Phone = phone?.Trim();
        TaxNumber = taxNumber?.Trim();
        CreditLimit = creditLimit;        // ← was missing
        DelaiRetour = delaiRetour;        // ← was missing
        DuePaymentPeriod = duePaymentPeriod; // ← was missing
        IsBlocked = isBlocked;            // ← was missing
        IsDeleted = isDeleted;            // ← was missing
        CreatedAt = createdAt ?? CreatedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public ClientCategoryCache AddCategory(CategoryCache category, Guid assignedById)
    {
        GuardNotDeleted();
        if (!category.IsActive)
            throw new InvalidOperationException(
                $"Category '{category.Name}' is not active.");

        if (ClientCategories.Any(cc => cc.CategoryId == category.Id))
            throw new InvalidOperationException(
                $"Client already has category '{category.Name}'.");

        ClientCategoryCache clientCategory = ClientCategoryCache.Create(Id, category.Id);
        ClientCategories.Add(clientCategory);

        UpdatedAt = DateTime.UtcNow;
        return clientCategory;
    }

    public void RemoveCategory(CategoryCache category)
    {
        GuardNotDeleted();

        ClientCategoryCache? existing = ClientCategories
            .FirstOrDefault(cc => cc.CategoryId == category.Id);

        if (existing is null)
            throw new InvalidOperationException(
                $"Client does not have category '{category.Name}'.");

        ClientCategories.Remove(existing);

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCreditLimit(decimal limit)
    {
        CreditLimit = limit;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveCreditLimit()
    {
        CreditLimit = null;  // ← Now works because CreditLimit is nullable
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDelaiRetour(int days)
    {
        DelaiRetour = days;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearDelaiRetour()
    {
        DelaiRetour = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Block()
    {
        GuardNotDeleted();
        if (IsBlocked) return;
        IsBlocked = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unblock()
    {
        if (!IsBlocked) return;
        IsBlocked = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public int? GetEffectiveDelaiRetour()
    {
        if (DelaiRetour.HasValue) return DelaiRetour.Value;

        int categoryMax = ClientCategories
            .Select(cc => cc.Category)
            .Where(c => c is { IsActive: true, IsDeleted: false })
            .Select(c => c.DelaiRetour)
            .DefaultIfEmpty(0)
            .Max();

        return categoryMax > 0 ? categoryMax : null;
    }

    public decimal? GetEffectiveCreditLimit()  // ← Returns nullable decimal
    {
        // If no base credit limit, return null
        if (!CreditLimit.HasValue || CreditLimit.Value <= 0)
            return null;

        // Get the highest multiplier from active categories
        decimal multiplier = ClientCategories
            .Select(cc => cc.Category)
            .Where(c => c is { IsActive: true, IsDeleted: false } && c.CreditLimitMultiplier.HasValue)
            .Select(c => c.CreditLimitMultiplier!.Value)  // Use ! after filtering
            .DefaultIfEmpty(1m)  // Default to 1 if no multipliers found
            .Max();

        return CreditLimit.Value * multiplier;
    }

    public void SetDuePaymentPeriod(int days)
    {
        DuePaymentPeriod = days;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearDuePaymentPeriod()
    {
        DuePaymentPeriod = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public int GetEffectiveDuePaymentPeriod()
    {
        if (DuePaymentPeriod.HasValue) return DuePaymentPeriod.Value;

        int categoryMax = ClientCategories
            .Select(cc => cc.Category)
            .Where(c => c is { IsActive: true, IsDeleted: false })
            .Select(c => c.DuePaymentPeriod)
            .DefaultIfEmpty(0)
            .Max();

        return categoryMax;
    }

    public bool CanPlaceOrder(decimal orderAmount, decimal currentBalance)
    {
        if (IsBlocked || IsDeleted) return false;

        decimal? limit = GetEffectiveCreditLimit();

        // If no credit limit set, allow order
        if (!limit.HasValue) return true;

        return currentBalance + orderAmount <= limit.Value;
    }

    public bool IsWithinDelaiRetour(DateTime documentDate)
    {
        int? window = GetEffectiveDelaiRetour();
        if (!window.HasValue) return false;
        return (DateTime.UtcNow - documentDate).TotalDays <= window.Value;
    }

    private void GuardNotDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted client.");
    }
}