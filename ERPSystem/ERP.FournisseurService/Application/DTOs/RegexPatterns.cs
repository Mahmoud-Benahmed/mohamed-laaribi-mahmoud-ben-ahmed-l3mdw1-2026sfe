namespace ERP.FournisseurService.Application.DTOs;

public static class RegexPatterns
{
    public const string SafeText = @"^[\p{L}0-9\s,.'\-]+$";
    public const string Phone = @"^\+?[\d][\d\s]{6,18}[\d]$";
    public const string AlphaNumeric = @"^[A-Za-z0-9]+$";
}