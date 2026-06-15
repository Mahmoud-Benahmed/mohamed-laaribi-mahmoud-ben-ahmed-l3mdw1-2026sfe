using ERP.PaymentService.Application.DTO;
using ERP.PaymentService.Domain;

namespace ERP.PaymentService.Application.Interfaces;

public interface IPaymentRepository
{
    Task<PaymentStatsDto> GetStatsAsync();
    Task<Payment?> GetByIdAsync(Guid id);
    Task<Payment?> GetByNumberAsync(string number);
    Task<List<Payment>> GetByClientIdAsync(Guid clientId); 
    Task<(List<Payment> Items, int TotalCount)> GetPagedByClientIdAsync(
    Guid clientId, int pageNumber, int pageSize);
    Task<(List<Payment> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, PaymentStatus status, string? search = null);
    Task<List<PaymentSummaryDto>> GetSummaryByInvoiceIdAsync(Guid invoiceId);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
    Task SaveChangesAsync();
}

public interface IPaymentInvoiceRepository
{
    Task<PaymentInvoice?> GetByIdAsync(Guid id);
    Task<List<PaymentInvoice>> GetByPaymentIdAsync(Guid paymentId);
    Task<List<PaymentInvoice>> GetByInvoiceIdAsync(Guid invoiceId);
    Task AddAsync(PaymentInvoice paymentInvoice);
    Task SaveChangesAsync();
    Task DeleteAsync(PaymentInvoice paymentInvoice);
}

public interface IRefundRequestRepository
{
    Task<RefundStatsDto> GetStatsAsync();
    Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RefundRequest?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<List<RefundRequest>> GetByClientIdAsync(Guid clientId);
    Task AddAsync(RefundRequest refund, CancellationToken ct = default);
    void Update(RefundRequest refund);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}