namespace ERP.ClientService.Domain;

public sealed class Category
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public Guid? TenantId { get; init; }
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

    // ── Relationships — read-only from Category side ──────────────────────────
    // Client owns this relationship. Category exposes it as read-only.
    private readonly List<ClientCategory> _clientCategories = [];
    public IReadOnlyCollection<ClientCategory> ClientCategories =>
        _clientCategories.AsReadOnly();

    // ── EF Core constructor ───────────────────────────────────────────────────
    private Category() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Category Create(
        string name,
        string code,
        int delaiRetour,
        int duePaymentPeriod,
        bool useBulkPricing = false,
        decimal? discountRate = null,
        decimal? creditLimitMultiplier = null,
        Guid? tenantId= null
        )
    {
        ValidateName(name);
        ValidateCode(code);
        ValidateDelaiRetour(delaiRetour);
        ValidateDiscountRate(discountRate);
        ValidateCreditLimitMultiplier(creditLimitMultiplier);
        ValidateDuePaymentPeriod(duePaymentPeriod);

        return new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            DelaiRetour = delaiRetour,
            UseBulkPricing = useBulkPricing,
            DiscountRate = discountRate,
            CreditLimitMultiplier = creditLimitMultiplier,
            DuePaymentPeriod = duePaymentPeriod,
            CreatedAt = DateTime.UtcNow,
        };
    }


    // ── Update ────────────────────────────────────────────────────────────────
    public void Update(
    string name,
    string code,
    int delaiRetour,
    int duePaymentPeriod,          // ← int, not int?
    bool useBulkPricing = false,
    decimal? discountRate = null,
    decimal? creditLimitMultiplier = null)
    {
        GuardNotDeleted();
        ValidateName(name);
        ValidateCode(code);
        ValidateDelaiRetour(delaiRetour);
        ValidateDuePaymentPeriod(duePaymentPeriod);
        ValidateDiscountRate(discountRate);
        ValidateCreditLimitMultiplier(creditLimitMultiplier);

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        DelaiRetour = delaiRetour;
        DuePaymentPeriod = duePaymentPeriod;
        UseBulkPricing = useBulkPricing;
        DiscountRate = discountRate;
        CreditLimitMultiplier = creditLimitMultiplier;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────
    public void Activate()
    {
        GuardNotDeleted();
        if (IsActive) return;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        GuardNotDeleted();
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Bulk pricing toggle ───────────────────────────────────────────────────
    public void EnableBulkPricing()
    {
        if (UseBulkPricing) return;
        UseBulkPricing = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableBulkPricing()
    {
        if (!UseBulkPricing) return;
        UseBulkPricing = false;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Soft delete / Restore ─────────────────────────────────────────────────
    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Domain query helpers ──────────────────────────────────────────────────

    public decimal ApplyDiscount(decimal price)
    {
        if (!DiscountRate.HasValue) return price;
        return price * (1 - DiscountRate.Value);
    }

    public decimal GetEffectiveCredit(decimal baseCredit)
    {
        if (!CreditLimitMultiplier.HasValue) return baseCredit;
        return baseCredit * CreditLimitMultiplier.Value;
    }

    public bool IsWithinDelaiRetour(DateTime documentDate) =>
        (DateTime.UtcNow - documentDate).TotalDays <= DelaiRetour;

    // ── Private guards and validators ─────────────────────────────────────────

    private void GuardNotDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted category.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Trim().Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters.", nameof(name));
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));
        if (code.Trim().Length > 50)
            throw new ArgumentException("Code cannot exceed 50 characters.", nameof(code));
    }

    private static void ValidateDelaiRetour(int delaiRetour)
    {
        if (delaiRetour <= 0)
            throw new ArgumentException(
                "Return delay must be at least 1 day.", nameof(delaiRetour));
    }

    private static void ValidateDiscountRate(decimal? discountRate)
    {
        if (!discountRate.HasValue) return;
        if (discountRate < 0 || discountRate > 1)
            throw new ArgumentException(
                "Discount rate must be between 0 and 1 (0% – 100%).",
                nameof(discountRate));
    }

    private static void ValidateCreditLimitMultiplier(decimal? multiplier)
    {
        if (!multiplier.HasValue) return;
        if (multiplier < 0)
            throw new ArgumentException(
                "Credit limit multiplier must be positive.",
                nameof(multiplier));
    }

    private static void ValidateDuePaymentPeriod(int days)
    {
        if (days <= 0)
            throw new ArgumentException(
                "Due payment period must be at least 1 day.", nameof(days));
    }
}