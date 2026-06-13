// InvoiceService.Infrastructure.Persistence/InvoiceNumberGenerator.cs
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.InvoiceService.Infrastructure.Persistence;

public class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly InvoiceDbContext _context;
    private static readonly object _lock = new object();

    public InvoiceNumberGenerator(InvoiceDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextInvoiceNumberAsync(Guid? tenantId)
    {
        int currentYear = DateTime.UtcNow.Year;

        // Use a distributed lock or database transaction
        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Get or create sequence with a lock
            InvoiceSequence? sequence = await _context.InvoiceSequences
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (sequence == null)
            {
                sequence = new InvoiceSequence(currentYear, tenantId);
                _context.InvoiceSequences.Add(sequence);
                await _context.SaveChangesAsync();
            }
            else
            {
                await _context.Entry(sequence)
                              .ReloadAsync();
            }
            
            Console.WriteLine("Sequence Before GetNextNumber(): {0}", sequence.CurrentNumber);

            int nextNumber = sequence.GetNextNumber();
            string invoiceNumber = sequence.FormatInvoiceNumber();

            Console.WriteLine("Sequence After GetNextNumber(): {0}", sequence.CurrentNumber);


            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return invoiceNumber;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            // Retry once on concurrency conflict
            return await GenerateNextInvoiceNumberAsync(tenantId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}