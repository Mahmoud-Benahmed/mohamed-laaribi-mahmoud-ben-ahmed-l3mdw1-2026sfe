using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Application.Services;
using ERP.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.PaymentService.Infrastructure.Persistence;

public class PaymentNumberGenerator : IPaymentNumberGenerator
{
    private readonly PaymentDbContext _context;
    public readonly ITenantContext _tenantContext;

    public PaymentNumberGenerator(PaymentDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<string> GenerateNextPaymentNumberAsync()
    {
        int currentYear = DateTime.UtcNow.Year;

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            PaymentSequence? sequence = await _context.PaymentSequences
                .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId);

            if (sequence is null)
            {
                sequence = new PaymentSequence(currentYear, _tenantContext.TenantId);
                _context.PaymentSequences.Add(sequence);
                await _context.SaveChangesAsync();
            }
            else
            {
                await _context.Entry(sequence).ReloadAsync();
            }

            sequence.GetNextNumber();
            string paymentNumber = sequence.FormatPaymentNumber();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return paymentNumber;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return await GenerateNextPaymentNumberAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}