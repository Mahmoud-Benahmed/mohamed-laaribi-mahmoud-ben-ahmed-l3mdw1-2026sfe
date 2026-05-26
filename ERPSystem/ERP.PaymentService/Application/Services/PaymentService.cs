using ERP.PaymentService.Application.DTO;
using ERP.PaymentService.Application.Exceptions;
using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Application.Interfaces.LocalCache;
using ERP.PaymentService.Domain;
using ERP.PaymentService.Infrastructure.Messaging;
using ERP.PaymentService.Infrastructure.Persistence.Repositories;
using System.ComponentModel.Design;

namespace ERP.PaymentService.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentInvoiceRepository _paymentInvoiceRepo;

    private readonly IInvoiceCacheRepository _invoiceCacheRepository;
    private readonly IPaymentNumberGenerator _numberGenerator;
    private readonly ILogger<PaymentService> _logger;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;

    public PaymentService(
        IPaymentInvoiceRepository paymentInvoiceRepo,
        IPaymentRepository paymentRepository,
        IInvoiceCacheRepository invoiceCacheRepository,
        IPaymentNumberGenerator numberGenerator,
        ILogger<PaymentService> logger,
        IEventPublisher eventPublisher,
        ITenantContext tenantContext)
    {
        _numberGenerator = numberGenerator;
        _paymentRepository = paymentRepository;
        _invoiceCacheRepository = invoiceCacheRepository;
        _logger = logger;
        _eventPublisher = eventPublisher;
        _paymentInvoiceRepo = paymentInvoiceRepo;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentStatsDto> GetStatsAsync()
    {
        return await _paymentRepository.GetStatsAsync();
    }
    public async Task<PaymentDto> GetByIdAsync(Guid id)
    {
        var payment = await _paymentRepository.GetByIdAsync(id) ?? throw new PaymentNotFoundException(id);
        return ToDto(payment);
    }

    public async Task<PaymentDto> GetByNumberAsync(string number)
    {
        var payment = await _paymentRepository.GetByNumberAsync(number) ?? throw new PaymentNotFoundException(number);
        return ToDto(payment);
    }

    public async Task<PagedResultDto<PaymentDto>> GetByClientIdAsync(Guid clientId, int pageNumber, int pageSize)
    {
        var items = await _paymentRepository.GetByClientIdAsync(clientId);

        return new PagedResultDto<PaymentDto>(
            items.Select(ToDto).ToList(),
            items.Count,
            pageNumber,
            pageSize);
    }

    public async Task<PagedResultDto<PaymentDto>> GetPagedAsync(
        int pageNumber, int pageSize, PaymentStatus status, string? search = null)
    {
        var (items, totalCount) = await _paymentRepository.GetPagedAsync(
            pageNumber, pageSize, status, search);

        return new PagedResultDto<PaymentDto>(
            items.Select(ToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<List<PaymentSummaryDto>> GetSummaryByInvoiceIdAsync(Guid invoiceId)
    {
        // verify invoice exists in cache before querying payments
        var invoiceExists = await _invoiceCacheRepository.GetByIdAsync(invoiceId);
        if (invoiceExists is null)
        {
            _logger.LogWarning(
                "Invoice {InvoiceId} not found in cache.", invoiceId);
            return [];
        }

        return await _paymentRepository.GetSummaryByInvoiceIdAsync(invoiceId);
    }

    public async Task<PaymentDto> CreateAsync(CreatePaymentDto dto)
    {
        // Round all incoming amounts first
        dto = dto with
        {
            TotalAmount = Math.Round(dto.TotalAmount, 2),
            Allocations = dto.Allocations.Select(a => a with
            {
                AmountAllocated = Math.Round(a.AmountAllocated, 2)
            }).ToList()
        };

        var allocationsSum = dto.Allocations.Sum(a => a.AmountAllocated);
        if (Math.Abs(allocationsSum - dto.TotalAmount) > 0.01m)
            throw new PaymentDomainException(
                $"TotalAmount ({dto.TotalAmount:F2}) must equal the sum of all allocations ({allocationsSum:F2}).");

        var invoiceIds = dto.Allocations.Select(a => a.InvoiceId).Distinct().ToList();
        var cacheEntries = new List<InvoiceCache>();

        foreach (var invoiceId in invoiceIds)
        {
            var cache = await _invoiceCacheRepository.GetByIdAsync(invoiceId);
            if (cache is null)
                throw new InvoiceNotFoundException(invoiceId);
            if (cache.Status == InvoiceStatus.CANCELLED)
                throw new InvoiceAlreadyCancelledException(invoiceId);
            if (cache.Status == InvoiceStatus.PAID)
                throw new InvoiceAlreadyPaidException(invoiceId);

            cacheEntries.Add(cache);
        }

        string paymentNumber = await _numberGenerator.GenerateNextPaymentNumberAsync();

        var payment = new Payment(
            number: paymentNumber,
            clientId: dto.ClientId,
            totalAmount: Math.Round(dto.TotalAmount, 2),
            method: dto.Method,
            paymentDate: dto.PaymentDate,
            externalReference: dto.ExternalReference,
            notes: dto.Notes,
            tenantId: _tenantContext.TenantId
        );

        foreach (var allocation in dto.Allocations)
        {
            var cache = cacheEntries.First(c => c.Id == allocation.InvoiceId);
            payment.AllocateAmount(Math.Round(allocation.AmountAllocated, 2), cache);
        }

        await _paymentRepository.AddAsync(payment);

        _logger.LogInformation(
            "Payment {Number} created. Id: {PaymentId}, TotalAmount: {TotalAmount}, Allocations: {Count}",
            payment.Number, payment.Id, payment.TotalAmount, payment.Allocations.Count);

        foreach (var allocation in dto.Allocations)
        {
            var cache = cacheEntries.First(c => c.Id == allocation.InvoiceId);
            cache.ApplyPayment(Math.Round(allocation.AmountAllocated, 2));
            await _invoiceCacheRepository.SaveChangesAsync(cache);

            if (cache.Status == InvoiceStatus.PAID)
            {
                await _eventPublisher.PublishAsync(
                    PaymentTopics.InvoicePaid,
                    new InvoicePaidEvent(
                        InvoiceId: cache.Id,
                        PaymentId: payment.Id,
                        PaidAmount: Math.Round(cache.PaidAmount, 2),
                        PaidAt: DateTime.UtcNow
                    ));

                _logger.LogInformation(
                    "Invoice {InvoiceId} fully paid via Payment {PaymentId}.",
                    cache.Id, payment.Id);
            }
        }

        return ToDto(payment);
    }


    public async Task<PaymentDto> CorrectDetailsAsync(Guid id, CorrectPaymentDto dto)
    {
        var payment = await _paymentRepository.GetByIdAsync(id) ?? throw new PaymentNotFoundException(id);

        payment.CorrectDetails(dto.PaymentDate, dto.Method, dto.ExternalReference, dto.Notes);

        await _paymentRepository.UpdateAsync(payment);

        return ToDto(payment);
    }

    public async Task CancelAsync(Guid id)
    {
        var payment = await _paymentRepository.GetByIdAsync(id)
            ?? throw new PaymentNotFoundException(id);

        payment.Cancel();
        await _paymentRepository.UpdateAsync(payment);

        foreach (PaymentInvoice allocation in payment.Allocations)
        {
            var cache = await _invoiceCacheRepository.GetByIdAsync(allocation.InvoiceId);
            if (cache is not null)
            {
                var remaining = Math.Round(
                    Math.Max(0m, allocation.AmountAllocated - allocation.RefundedAmount),
                    2,
                    MidpointRounding.AwayFromZero
                );

                cache.ReversePayment(remaining);

                await _invoiceCacheRepository.SaveChangesAsync(cache);

                await _eventPublisher.PublishAsync(PaymentTopics.Cancelled,
                new PaymentCancelledEvent(
                    PaymentId: payment.Id,
                    InvoiceId: allocation.InvoiceId,
                    ReversedAmount: allocation.AmountAllocated,
                    CancelledAt: payment.CancelledAt.Value));
            }
        }

        await _paymentInvoiceRepo.SaveChangesAsync();

        _logger.LogInformation(
            "Payment {PaymentId} cancelled at {CancelledAt}.",
            id, payment.CancelledAt);
    }

    // ── mapping ────────────────────────────────────────────────

    // ── mapping ────────────────────────────────────────────────────────────────
    private static PaymentDto ToDto(Payment p) => new(
        Id: p.Id,
        Number: p.Number,
        ClientId: p.ClientId,
        TotalAmount: Math.Round(p.TotalAmount, 2),
        RemainingAmount: Math.Round(p.GetRemainingAmount(), 2),
        Method: p.Method.ToString(),
        PaymentDate: p.PaymentDate,
        Status: p.Status,
        ExternalReference: p.ExternalReference,
        Notes: p.Notes,
        IsCancelled: p.CancelledAt is not null,
        CancelledAt: p.CancelledAt,
        Allocations: p.Allocations.Select(a => new PaymentAllocationDto(
            Id: a.Id,
            InvoiceId: a.InvoiceId,
            AmountAllocated: Math.Round(a.AmountAllocated, 2)
        )).ToList()
    );
}