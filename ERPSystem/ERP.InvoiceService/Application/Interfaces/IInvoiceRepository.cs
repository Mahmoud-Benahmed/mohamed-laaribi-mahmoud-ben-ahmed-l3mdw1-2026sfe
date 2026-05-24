using InvoiceService.Domain;

namespace InvoiceService.Application.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<List<Invoice>> GetOverdueAsync(DateTime asOf);
        Task<bool> PenaltyExistsForInvoiceAsync(string originalInvoiceNumber);

        // ── Existing queries ─────────────────────────────────────────────────────
        Task<Invoice?> GetByIdAsync(Guid id);
        Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task<(List<Invoice> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, bool includeDeleted = false);
        Task<(List<Invoice> Items, int TotalCount)> GetByClientIdAsync(Guid clientId, int pageNumber, int pageSize);
        Task<(List<Invoice> Items, int TotalCount)> GetByStatusAsync(InvoiceStatus status, int pageNumber, int pageSize);
        Task AddAsync(Invoice invoice);
        Task UpdateAsync(Invoice invoice);
        Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber);
        Task<List<Invoice>> GetByClientIdAsNoTrackingAsync(Guid clientId);

        // ── Stats queries ────────────────────────────────────────────────────────
        Task<List<InvoiceStatProjection>> GetStatsProjectionAsync();
        Task<int> GetDeletedCountAsync();

        // ── Nested projection read-model ─────────────────────────────────────────
        public class InvoiceStatProjection
        {
            public Guid Id { get; init; }
            public InvoiceStatus Status { get; init; }
            public DateTime InvoiceDate { get; init; }
            public DateTime DueDate { get; init; }
            public decimal TotalHT { get; init; }
            public decimal TotalTTC { get; init; }
            public decimal TotalTVA { get; init; }
            public Guid ClientId { get; init; }
            public string ClientFullName { get; init; } = string.Empty;
        }
    }
}