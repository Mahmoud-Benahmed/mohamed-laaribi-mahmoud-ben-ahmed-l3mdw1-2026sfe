using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.PaymentService.Infrastructure.Persistence.Repositories;

public class PaymentInvoiceRepository : IPaymentInvoiceRepository
{
    private readonly PaymentDbContext _context;

    public PaymentInvoiceRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentInvoice?> GetByIdAsync(Guid id)
    {
        return await _context.PaymentsInvoices.AsNoTracking()
            .FirstOrDefaultAsync(pi => pi.Id == id);
    }

    public async Task<List<PaymentInvoice>> GetByPaymentIdAsync(Guid paymentId)
    {
        return await _context.PaymentsInvoices.AsNoTracking()
            .Where(pi => pi.PaymentId == paymentId)
            .ToListAsync();
    }

    public async Task<List<PaymentInvoice>> GetByInvoiceIdAsync(Guid invoiceId)
    {
        return await _context.PaymentsInvoices.AsNoTracking()
            .Where(pi => pi.InvoiceId == invoiceId)
            .Join(_context.Payments,
                  pi => pi.PaymentId,
                  p => p.Id,
                  (pi, p) => new { pi, p })
            .Where(x => x.p.Status == PaymentStatus.DONE)
            .Select(x => x.pi)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task AddAsync(PaymentInvoice paymentInvoice)
    {
        await _context.PaymentsInvoices.AddAsync(paymentInvoice);
    }

    public Task DeleteAsync(PaymentInvoice paymentInvoice)
    {
        _context.PaymentsInvoices.Remove(paymentInvoice);
        return Task.CompletedTask;
    }
}
