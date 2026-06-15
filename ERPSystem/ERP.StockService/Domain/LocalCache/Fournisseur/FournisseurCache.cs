using ERP.StockService.Application.DTOs;

namespace ERP.StockService.Domain.LocalCache.Fournisseur;

public sealed class FournisseurCache
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? TaxNumber { get; private set; }
    public string RIB { get; private set; } = default!;
    public bool IsDeleted { get; private set; }
    public bool IsBlocked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Parameterless constructor for EF Core
    private FournisseurCache() { }

    // Constructor for creating from DTO
    public FournisseurCache(FournisseurResponseDto dto)
    {
        Id = dto.Id;
        TenantId = dto.TenantId;
        Name = dto.Name ?? throw new ArgumentNullException(nameof(dto.Name));
        Address = dto.Address ?? throw new ArgumentNullException(nameof(dto.Address));
        Phone = dto.Phone ?? throw new ArgumentNullException(nameof(dto.Phone));
        Email = dto.Email;
        TaxNumber = dto.TaxNumber;
        RIB = dto.RIB ?? throw new ArgumentNullException(nameof(dto.RIB));
        IsDeleted = dto.IsDeleted;
        IsBlocked = dto.IsBlocked;
        CreatedAt = dto.CreatedAt;
        UpdatedAt = dto.UpdatedAt;
    }

    // Static factory method
    public static FournisseurCache FromEvent(FournisseurResponseDto dto)
    {
        return new FournisseurCache
        {
            Id = Guid.NewGuid(),
            TenantId = dto.TenantId,
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            Phone = dto.Phone.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            RIB = dto.RIB.Trim(),
            Email = dto.Email?.Trim(),
            IsBlocked = dto.IsBlocked,
            IsDeleted = dto.IsDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // ---------------- UPDATE ----------------
    public void Update(
        string name, string address, string phone, string rib, 
        string? email = null,
        string? taxNumber= null)
    {
        Name = name.Trim();
        Address = address.Trim();
        Phone = phone.Trim();
        TaxNumber = taxNumber?.Trim();
        RIB = rib.Trim();
        Email = email?.Trim();
    }

    // Apply update from DTO
    public void ApplyUpdate(FournisseurResponseDto dto)
    {
        Name = dto.Name;
        Address = dto.Address;
        Phone = dto.Phone;
        Email = dto.Email;
        TaxNumber = dto.TaxNumber;
        RIB = dto.RIB;
        IsBlocked = dto.IsBlocked;
        IsDeleted = dto.IsDeleted;
        UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow;
    }

    public void MarkDeleted() => IsDeleted = true;
    public void MarkRestored() => IsDeleted = false;
    public void Block() => IsBlocked = true;
    public void Unblock() => IsBlocked = false;
}