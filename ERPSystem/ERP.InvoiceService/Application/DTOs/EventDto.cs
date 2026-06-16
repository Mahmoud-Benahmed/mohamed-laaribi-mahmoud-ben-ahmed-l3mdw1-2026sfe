using static ERP.InvoiceService.Properties.ApiRoutes.Invoices;

namespace ERP.InvoiceService.Application.DTOs;

// CLient DTOs
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
string? Email, string TaxNumber, string RIB,
bool IsDeleted, bool IsBlocked,
DateTime CreatedAt, DateTime? UpdatedAt, Guid? TenantId);

public sealed record InvoiceEventDto(
    Guid Id,
    string InvoiceNumber,
    decimal TotalTTC,
    string Status,
    Guid ClientId,
    List<InvoiceItemEventDto> Items,
    Guid? TenantId
);

public record InvoiceItemEventDto(
    Guid ArticleId,
    decimal Quantity,
    decimal UniPriceHT,
    decimal TaxRate
);

public sealed record InvoicePaidEvent(
    Guid InvoiceId,
    Guid PaymentId,
    decimal PaidAmount,
    DateTime PaidAt, Guid? TenantId
);

public record PaymentCancelledEvent(
    Guid PaymentId,
    Guid InvoiceId,
    decimal ReversedAmount,
    DateTime CancelledAt, Guid? TenantId
);