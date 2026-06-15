namespace ERP.TenantService.Application.DTOs;

public static class RegexPatterns
{
    public const string SafeText = @"^[\p{L}0-9\s,.'\-]+$";
    public const string Phone = @"^\+?\d{8,15}$";
    public const string AlphaNumeric = @"^[A-Za-z0-9]+$";
    public const string CategoryCode = @"^[A-Za-z0-9_\-]+$";
    public const string SubdomainSlug = @"^[a-zA-Z0-9]{3,}$";
}