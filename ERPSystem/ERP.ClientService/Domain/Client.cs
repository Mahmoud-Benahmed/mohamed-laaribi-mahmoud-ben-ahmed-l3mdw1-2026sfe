namespace ERP.ClientService.Domain;

public class Client
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
    public List<ClientCategory> ClientCategories { get; private set; } = [];

    private Client() { }

    public static Client Create(
        string name, string email, string address,
        decimal? creditLimit = null,  // ← Made nullable with default
        string? phone = null, string? taxNumber = null,
        int? delaiRetour = null,
        int? duePaymentPeriod = null,
        Guid? tenantId = null
        )
    {
        ValidateName(name);
        ValidateEmail(email);
        ValidateAddress(address);
        ValidateCreditLimit(creditLimit);
        ValidateDelaiRetour(delaiRetour);
        ValidateDuePaymentPeriod(duePaymentPeriod);

        return new Client
        {
            Id = Guid.NewGuid(),
            TenantId= tenantId,
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Address = address.Trim(),
            Phone = phone?.Trim(),
            TaxNumber = taxNumber?.Trim(),
            CreditLimit = creditLimit,
            DelaiRetour = delaiRetour,
            DuePaymentPeriod = duePaymentPeriod,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Update(
        string name, string email, string address,
        string? phone = null, string? taxNumber = null)
    {
        GuardNotDeleted();
        ValidateName(name);
        ValidateEmail(email);
        ValidateAddress(address);

        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        Address = address.Trim();
        Phone = phone?.Trim();
        TaxNumber = taxNumber?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public ClientCategory AddCategory(Category category, Guid assignedById)
    {
        GuardNotDeleted();
        if (!category.IsActive)
            throw new InvalidOperationException(
                $"Category '{category.Name}' is not active.");

        if (ClientCategories.Any(cc => cc.CategoryId == category.Id))
            throw new InvalidOperationException(
                $"Client already has category '{category.Name}'.");

        ClientCategory clientCategory = ClientCategory.Create(Id, category.Id, assignedById, category);
        ClientCategories.Add(clientCategory);

        UpdatedAt = DateTime.UtcNow;
        return clientCategory;
    }

    public void RemoveCategory(Category category)
    {
        GuardNotDeleted();

        ClientCategory? existing = ClientCategories
            .FirstOrDefault(cc => cc.CategoryId == category.Id);

        if (existing is null)
            throw new InvalidOperationException(
                $"Client does not have category '{category.Name}'.");

        ClientCategories.Remove(existing);

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCreditLimit(decimal limit)
    {
        ValidateCreditLimit(limit);
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
        ValidateDelaiRetour(days);
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

        if (ClientCategories.Count == 0) return null;

        int categoryMax = ClientCategories
            .Where(cc => cc.Category is { IsActive: true, IsDeleted: false })
            .Select(cc => cc.Category!.DelaiRetour)
            .DefaultIfEmpty(0)
            .Max();

        return categoryMax > 0 ? categoryMax : null;
    }

    public decimal? GetEffectiveCreditLimit()  // ← Returns nullable decimal
    {
        if (!CreditLimit.HasValue || CreditLimit.Value <= 0)
            return null;

        decimal multiplier = ClientCategories
            .Where(cc => cc.Category is { IsActive: true, IsDeleted: false }
                      && cc.Category.CreditLimitMultiplier.HasValue)
            .Select(cc => cc.Category!.CreditLimitMultiplier!.Value)
            .DefaultIfEmpty(1m)
            .Max();

        return CreditLimit.Value * multiplier;
    }

    public void SetDuePaymentPeriod(int days)
    {
        ValidateDuePaymentPeriod(days);
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

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Trim().Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters.", nameof(name));
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (!email.Contains('@'))
            throw new ArgumentException("Email is not valid.", nameof(email));
    }

    private static void ValidateAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));
    }

    private static void ValidateCreditLimit(decimal? creditLimit)
    {
        if (creditLimit.HasValue && creditLimit <= 0)
            throw new ArgumentException("Credit limit must be positive.", nameof(creditLimit));
    }

    private static void ValidateDelaiRetour(int? days)
    {
        if (days.HasValue && days <= 0)
            throw new ArgumentException("Return delay must be at least 1 day.", nameof(days));
    }

    private static void ValidateDuePaymentPeriod(int? days)
    {
        if (days.HasValue && days <= 0)
            throw new ArgumentException(
                "Due payment period must be at least 1 day.", nameof(days));
    }
}