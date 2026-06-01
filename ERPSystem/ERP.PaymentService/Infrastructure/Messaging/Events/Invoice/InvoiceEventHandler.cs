using ERP.PaymentService.Application.DTO;
using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Application.Interfaces.LocalCache;
using ERP.PaymentService.Application.Services.LocalCache;
using ERP.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.PaymentService.Infrastructure.Messaging.Events.Invoice;

public sealed class InvoiceEventHandler : IInvoiceEventHandler
{
    private readonly IInvoiceCacheService _invoiceService;
    private readonly IRefundService _refundService;
    private readonly IRefundRequestRepository _refundRepo;
    private readonly IPaymentInvoiceRepository _paymentInvoiceRepository;
    private readonly ILogger<InvoiceEventHandler> _logger;
    private readonly PaymentDbContext _context;

    public InvoiceEventHandler(IInvoiceCacheService cacheService,
                                IPaymentInvoiceRepository paymentInvoiceRepository,
                                IRefundService refundService,
                                ILogger<InvoiceEventHandler> logger,
                                IRefundRequestRepository refundRepo,
                                PaymentDbContext context)
    {
        _paymentInvoiceRepository = paymentInvoiceRepository;
        _refundService = refundService;
        _invoiceService = cacheService;
        _logger = logger;
        _refundRepo = refundRepo;
        _context = context;
    }

    public async Task HandleCreatedAsync(InvoiceEventDto dto)
    {
        _logger.LogInformation(
            "Invoice Created | InvoiceId: {InvoiceId} | ClientId: {ClientId}",
            dto.Id,
            dto.ClientId);

        await _invoiceService.SyncCreatedAsync(dto);
    }

    public async Task HandleCancelledAsync(InvoiceEventDto dto)
    {
        try
        {
            _logger.LogInformation(
            "Invoice Cancelled START | InvoiceId: {InvoiceId} | ClientId: {ClientId}",
            dto.Id,
            dto.ClientId);

            await _invoiceService.SyncCancelledAsync(dto);

            var allocations = await _paymentInvoiceRepository.GetByInvoiceIdAsync(dto.Id);

            if (allocations == null || !allocations.Any())
            {
                _logger.LogInformation(
                    "No allocations found for cancelled invoice {InvoiceId}. Nothing to refund.",
                    dto.Id);
                return;
            }

            var refundableAllocations = allocations
               .Where(a => Math.Round(a.AmountAllocated - a.RefundedAmount, 2) > 0)
               .ToList();

            if (!refundableAllocations.Any())
            {
                _logger.LogInformation(
                    "All allocations for invoice {InvoiceId} already fully refunded. Skipping.",
                    dto.Id);
                return;
            }

            var existingRefund = await _refundRepo.GetByInvoiceIdAsync(dto.Id);
            if (existingRefund is not null)
            {
                _logger.LogWarning(
                    "Refund already exists for invoice {InvoiceId} (RefundId: {RefundId}). Skipping.",
                    dto.Id, existingRefund.Id);
                return;
            }


            await using var tx = await _context.Database.BeginTransactionAsync();

            var refund = new RefundRequest(dto.ClientId, dto.Id, $"InvoiceCancellation, Invoice number: {dto.InvoiceNumber}", dto.TenantId);

            foreach (var alloc in refundableAllocations)
            {
                var raw = alloc.AmountAllocated - alloc.RefundedAmount;

                var refundable = Math.Round(
                    Math.Max(0m, raw),
                    2,
                    MidpointRounding.AwayFromZero
                );

                if (refundable <= 0)
                    continue;

                refund.AddLine(
                    alloc.PaymentId,
                    alloc.Id,
                    refundable
                );

                alloc.Refund(refundable);
            }

            if (!refund.Lines.Any())
                return;

            await _refundRepo.AddAsync(refund);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Created refund {RefundId} for cancelled invoice {InvoiceId} " +
                "with {LineCount} line(s) totalling {Total:F2}.",
                refund.Id, dto.Id,
                refund.Lines.Count,
                refund.Lines.Sum(l => l.Amount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling InvoiceCancelledEvent — InvoiceId: {InvoiceId}", dto.Id);
            throw;
        }
    }
}