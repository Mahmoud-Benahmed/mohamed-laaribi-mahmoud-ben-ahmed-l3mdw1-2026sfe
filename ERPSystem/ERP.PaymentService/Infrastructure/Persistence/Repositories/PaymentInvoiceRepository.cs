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
        return await _context.PaymentsInvoices
            .FirstOrDefaultAsync(pi => pi.Id == id);
    }

    public async Task<List<PaymentInvoice>> GetByPaymentIdAsync(Guid paymentId)
    {
        return await _context.PaymentsInvoices
            .Where(pi => pi.PaymentId == paymentId)
            .ToListAsync();
    }

    public async Task<List<PaymentInvoice>> GetByInvoiceIdAsync(Guid invoiceId)
    {
        return await _context.PaymentsInvoices
            .Where(pi => pi.InvoiceId == invoiceId &&
                         _context.Payments.Any(p => p.Id == pi.PaymentId && p.Status == PaymentStatus.DONE))
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task AddAsync(PaymentInvoice paymentInvoice)
    {
        await _context.PaymentsInvoices.AddAsync(paymentInvoice);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var existing = await _context.PaymentsInvoices.FindAsync(id);

        if (existing is null)
            throw new InvalidOperationException(
                $"PaymentInvoice with Id {id} not found.");

        _context.PaymentsInvoices.Remove(existing);
        await _context.SaveChangesAsync();
    }
}
