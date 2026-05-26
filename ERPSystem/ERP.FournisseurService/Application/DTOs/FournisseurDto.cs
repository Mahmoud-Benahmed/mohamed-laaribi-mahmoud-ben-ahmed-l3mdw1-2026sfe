using ERP.FournisseurService.Domain;
using System.ComponentModel.DataAnnotations;

namespace ERP.FournisseurService.Application.DTOs;

// ── Fournisseur ───────────────────────────────────────────────────────────────
public record CreateFournisseurRequestDto(
    [Required][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")] string Name,

    [Required][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")] string Address,

    [Required] [RegularExpression(RegexPatterns.Phone, ErrorMessage = "Phone must contain digits and may start with +.")] [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    string Phone,

    [Required][MaxLength(50)][RegularExpression(RegexPatterns.AlphaNumeric , ErrorMessage = "Invalid tax number.")] string TaxNumber,

    [Required][MinLength(10)][MaxLength(50)][RegularExpression(RegexPatterns.AlphaNumeric , ErrorMessage = "Invalid RIB")] string RIB,

    [EmailAddress(ErrorMessage = "Invalid email format.")][MaxLength(200)] string? Email = null
);

public record UpdateFournisseurRequestDto(
    [Required][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")] string Name,
    
    [Required][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")] string Address,
    
    [RegularExpression(RegexPatterns.Phone, ErrorMessage = "Phone must contain digits and may start with +.")] [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    string Phone,
    
    [Required][MaxLength(50)][RegularExpression(RegexPatterns.AlphaNumeric, ErrorMessage = "Invalid tax number.")] string TaxNumber,

    [Required][MinLength(10)][MaxLength(50)][RegularExpression(RegexPatterns.AlphaNumeric, ErrorMessage ="Invalid RIB")] string RIB,
    
    [EmailAddress(ErrorMessage = "Invalid email format.")][MaxLength(200)] string? Email = null
    );

public sealed record FournisseurResponseDto(
    Guid Id, string Name, string Address, string Phone,
    string? Email, string TaxNumber, string RIB,
    bool IsDeleted, bool IsBlocked,
    DateTime CreatedAt, DateTime? UpdatedAt, Guid? TenantId);

public sealed record FournisseurStatsDto(
    int TotalFournisseurs, int ActiveFournisseurs,
    int BlockedFournisseurs, int DeletedFournisseurs
);

public static class FournisseurMapping
{
    public static FournisseurResponseDto ToResponseDto(this Fournisseur fournisseur) =>
        new FournisseurResponseDto(
            fournisseur.Id,
            fournisseur.Name,
            fournisseur.Address,
            fournisseur.Phone,
            fournisseur.Email,
            fournisseur.TaxNumber,
            fournisseur.RIB,
            fournisseur.IsDeleted,
            fournisseur.IsBlocked,
            fournisseur.CreatedAt,
            fournisseur.UpdatedAt,
            fournisseur.TenantId
        );
}
