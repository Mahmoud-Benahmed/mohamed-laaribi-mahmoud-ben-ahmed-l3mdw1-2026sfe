using ERP.PaymentService.Application.DTO;
using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.PaymentService.Infrastructure.Persistence.Repositories
{
    public class RefundRequestRepository : IRefundRequestRepository
    {
        private readonly PaymentDbContext _context;

        public RefundRequestRepository(PaymentDbContext context)
        {
            _context = context;
        }

        public async Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Refunds
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id, ct);
        }
        public async Task<RefundStatsDto> GetStatsAsync()
        {
            var counts = await _context.Refunds
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int total = counts.Sum(x => x.Count);
            int pending = counts.FirstOrDefault(x => x.Status == RefundStatus.PENDING)?.Count ?? 0;
            int completed = counts.FirstOrDefault(x => x.Status == RefundStatus.COMPLETED)?.Count ?? 0;

            return new RefundStatsDto(total, pending, completed);
        }

        public async Task<RefundRequest?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
        {
            return await _context.Refunds
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.InvoiceId == invoiceId, ct);
        }

        public async Task<List<RefundRequest>> GetByClientIdAsync(Guid clientId)
            => await _context.Refunds
                .AsNoTracking()
                .Include(r => r.Lines)
                .Where(r => r.ClientId == clientId)
                .OrderByDescending(r => r.CompletedAt)
                .ToListAsync();
        
        public async Task AddAsync(RefundRequest refund, CancellationToken ct = default)
        {
            await _context.Refunds.AddAsync(refund, ct);
        }

        public void Update(RefundRequest refund)
        {
            _context.Refunds.Update(refund);
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Refunds
                .AnyAsync(r => r.Id == id, ct);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            return _context.SaveChangesAsync(ct);
        }
    }
}
