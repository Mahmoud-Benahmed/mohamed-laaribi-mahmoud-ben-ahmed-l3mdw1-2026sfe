namespace ERP.PaymentService.Application.DTO;

public sealed record InvoiceEventDto(
    Guid Id,
    string InvoiceNumber,
    decimal TotalTTC,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Status,
    Guid ClientId,
    Guid? TenantId
);

public sealed record InvoicePaidEvent(
    Guid InvoiceId,
    Guid PaymentId,
    decimal PaidAmount,
    DateTime PaidAt
);

public record PaymentCancelledEvent(
    Guid PaymentId,
    Guid InvoiceId,
    decimal ReversedAmount,
    DateTime CancelledAt
);