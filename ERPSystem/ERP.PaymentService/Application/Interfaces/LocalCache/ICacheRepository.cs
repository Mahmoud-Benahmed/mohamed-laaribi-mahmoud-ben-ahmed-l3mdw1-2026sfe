namespace ERP.PaymentService.Application.Interfaces.LocalCache;

public interface IInvoiceCacheRepository
{
    Task<InvoiceCache?> GetByIdAsync(Guid invoiceId);
    Task<(List<InvoiceCache> Items, int TotalCount)> GetByClientIdAsync(Guid clientId, int pageNumber, int pageSize);
    Task<(List<InvoiceCache> Items, int TotalCount)> GetByStatusAsync(InvoiceStatus status, int pageNumber, int pageSize);
    Task AddAsync(InvoiceCache cache);
    Task UpdateAsync(InvoiceCache cache);

    Task SaveChangesAsync();
    Task<(List<InvoiceCache> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search = null);
}
