using ERP.InvoiceService.Application.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace InvoiceService.Application.DTOs;

// ════════════════════════════════════════════════════════════════════════════
// READ DTOs
// ════════════════════════════════════════════════════════════════════════════

public record InvoiceDto(
    Guid Id,
    TaxCalculationMode TaxMode,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal DiscountRate,
    decimal TotalHT,
    decimal TotalTVA,
    decimal TotalTTC,
    string Status,
    Guid ClientId,
    string ClientFullName,
    string ClientAddress,
    string? AdditionalNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted,
    List<InvoiceItemDto> Items,
    Guid? TenantId
);

public record InvoiceItemDto(
    Guid Id,
    Guid ArticleId,
    string ArticleName,
    string ArticleBarCode,
    decimal Quantity,
    decimal UniPriceHT,
    decimal EffectivePriceHT,
    decimal TaxRate,
    decimal TotalHT,
    decimal TotalTTC
);

// ════════════════════════════════════════════════════════════════════════════
// CREATE / COMMAND DTOs
// ════════════════════════════════════════════════════════════════════════════
public record CreateInvoiceDto(
    [Required] DateTime InvoiceDate,
    [Required] DateTime DueDate,
    [Required(ErrorMessage = "Tax calculation mode is required.")] TaxCalculationMode TaxMode,
    [Required] Guid ClientId,
    [MaxLength(1000)][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters")] string? AdditionalNotes,
    [Required][MinLength(1)] List<CreateInvoiceItemDto> Items
);

public record CreateInvoiceItemDto(
    [Required] Guid ArticleId,
    [Required][Range(1, double.MaxValue)] decimal Quantity,
    [Required][Range(0, double.MaxValue)] decimal UniPriceHT,
    [Required][Range(0, 1)] decimal TaxRate
);

public record UpdateInvoiceDto(
    [Required] DateTime InvoiceDate,
    [Required] DateTime DueDate,
    [Required(ErrorMessage = "Tax calculation mode is required.")] TaxCalculationMode TaxMode,
    [Required] Guid ClientId,
    [MaxLength(1000)][RegularExpression(RegexPatterns.SafeText, ErrorMessage ="Invalid characters")] string? AdditionalNotes,
    [Required][MinLength(1)] List<UpdateInvoiceItemDto> Items
);

public record UpdateInvoiceItemDto(
    [Required] Guid ArticleId,
    [Required][Range(1, double.MaxValue)] decimal Quantity,
    [Required][Range(0, double.MaxValue)] decimal UniPriceHT,
    [Required][Range(0, 1)] decimal TaxRate
);

public record AddInvoiceItemDto(
    [Required] Guid ArticleId,
    [Required][Range(1, double.MaxValue)] decimal Quantity,
    [Required][Range(0, double.MaxValue)] decimal UniPriceHT,
    [Required][Range(0, 1)] decimal TaxRate
);

// ════════════════════════════════════════════════════════════════════════════
// STATS DTOs
// ════════════════════════════════════════════════════════════════════════════

public record InvoiceStatsDto(
    [Range(0, int.MaxValue)] int TotalInvoices,
    [Range(0, int.MaxValue)] int DraftCount,
    [Range(0, int.MaxValue)] int UnpaidCount,
    [Range(0, int.MaxValue)] int PaidCount,
    [Range(0, int.MaxValue)] int CancelledCount,
    [Range(0, int.MaxValue)] int DeletedCount,
    [Range(0, int.MaxValue)] int OverdueCount,
    [Range(0, double.MaxValue)] decimal TotalRevenueHT,
    [Range(0, double.MaxValue)] decimal TotalRevenueTTC,
    [Range(0, double.MaxValue)] decimal TotalTVACollected,
    [Range(0, double.MaxValue)] decimal OutstandingHT,
    [Range(0, double.MaxValue)] decimal OutstandingTTC,
    [Range(0, double.MaxValue)] decimal OverdueHT,
    [Range(0, double.MaxValue)] decimal OverdueTTC,
    [Range(0, double.MaxValue)] decimal AverageInvoiceValueHT,
    [Range(0, double.MaxValue)] double AveragePaymentDays,
    IReadOnlyList<ClientRevenueDto> TopClients,
    IReadOnlyList<MonthlyStatsDto> MonthlyBreakdown
);

public record ClientRevenueDto(
    [Required] Guid ClientId,
    [Required][MinLength(1)][MaxLength(200)] string ClientFullName,
    [Range(0, int.MaxValue)] int InvoiceCount,
    [Range(0, double.MaxValue)] decimal RevenueTTC
);

public record MonthlyStatsDto(
    [Range(1, 9999)] int Year,
    [Range(1, 12)] int Month,
    [Range(0, int.MaxValue)] int IssuedCount,
    [Range(0, int.MaxValue)] int PaidCount,
    [Range(0, double.MaxValue)] decimal IssuedTTC,
    [Range(0, double.MaxValue)] decimal PaidTTC
);

public sealed class PagedResultDto<T>
{
    public List<T> Items { get; }
    public int TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public PagedResultDto(List<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}