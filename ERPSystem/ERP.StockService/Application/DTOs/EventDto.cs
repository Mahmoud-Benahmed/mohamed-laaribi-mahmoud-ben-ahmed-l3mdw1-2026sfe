namespace ERP.StockService.Application.DTOs;

// Client DTOs
public sealed record ClientResponseDto(
    Guid Id,
    string Name,
    string Email,
    string Address,
    int DuePaymentPeriod,
    string? Phone,
    string? TaxNumber,
    decimal? CreditLimit,
    int? DelaiRetour,
    bool IsBlocked,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<ClientCategoryResponseDto> Categories,
    Guid? TenantId
);

public sealed record ClientCategoryResponseDto(
    Guid Id,
    string Name,
    string Code,
    int DelaiRetour,
    int DuePaymentPeriod,                   // ← added
    decimal? DiscountRate,
    decimal? CreditLimitMultiplier,
    bool UseBulkPricing,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? TenantId
);


// Article DTOs
public record ArticleCategoryResponseDto(
    Guid Id,
    string Name,
    decimal TVA,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? TenantId
);

public record ArticleResponseDto(
    Guid Id,
    ArticleCategoryResponseDto Category,
    string CodeRef,
    string BarCode,
    string Libelle,
    decimal Prix,
    string Unit,
    decimal TVA,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? TenantId
    );


public sealed record FournisseurResponseDto(
Guid Id, string Name, string Address, string Phone,
string? Email, string? TaxNumber, string RIB,
bool IsDeleted, bool IsBlocked,
DateTime CreatedAt, DateTime? UpdatedAt, Guid? TenantId);

public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
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
    decimal TaxRate,
    decimal TotalHT,
    decimal TotalTTC
);