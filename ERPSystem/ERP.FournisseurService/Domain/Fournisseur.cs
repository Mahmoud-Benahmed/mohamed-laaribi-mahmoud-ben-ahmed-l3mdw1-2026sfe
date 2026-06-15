using System.Net.Mail;

namespace ERP.FournisseurService.Domain;

public sealed class Fournisseur
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? TaxNumber { get; private set; } = default!;
    public string RIB { get; private set; } = default!;
    public bool IsDeleted { get; private set; }
    public bool IsBlocked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Fournisseur() { }

    // ---------------- CREATE ----------------
    // Fournisseur.cs
    public static Fournisseur Create(
        string name, string address, string phone,
        string rib, string? email = null, string? taxNumber = null, Guid? tenantId = null)
    {
        ValidateArgs(name, address, phone, taxNumber, rib, email);

        return new Fournisseur
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Address = address.Trim(),
            Phone = phone.Trim(),
            TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim(),
            RIB = rib.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Update(
        string name, string address, string phone, string rib,
        string? email = null, string? taxNumber = null)
    {
        GuardNotDeleted();
        ValidateArgs(name, address, phone, taxNumber, rib, email);

        Name = name.Trim();
        Address = address.Trim();
        Phone = phone.Trim();
        TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();
        RIB = rib.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    // ---------------- BLOCK / UNBLOCK ----------------
    public void Block()
    {
        GuardNotDeleted();
        if (IsBlocked) return;

        IsBlocked = true;
    }

    public void Unblock()
    {
        if (!IsBlocked) return;

        IsBlocked = false;
    }

    // ---------------- DELETE / RESTORE ----------------
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
    }

    // ---------------- GUARD ----------------
    private void GuardNotDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted supplier.");
    }

    // ---------------- VALIDATION ----------------
    private static void ValidateArgs(
        string name, string address, string phone,
        string? taxNumber, string rib, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone is required.", nameof(phone));

        if (string.IsNullOrWhiteSpace(rib))
            throw new ArgumentException("RIB is required.", nameof(rib));

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            throw new ArgumentException("Email is not valid.", nameof(email));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            MailAddress addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}