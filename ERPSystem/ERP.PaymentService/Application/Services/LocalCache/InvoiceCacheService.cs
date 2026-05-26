using ERP.PaymentService.Application.DTO;
using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Application.Interfaces.LocalCache;
using ERP.PaymentService.Infrastructure.Persistence.Repositories;
using static ERP.PaymentService.Properties.ApiRoutes;

namespace ERP.PaymentService.Application.Services.LocalCache;

public class InvoiceCacheService : IInvoiceCacheService
{
    private readonly IInvoiceCacheRepository _invoiceCacheRepository;

    private readonly ILogger<InvoiceCacheService> _logger;

    public InvoiceCacheService(
        IInvoiceCacheRepository invoiceCacheRepository,
        ILogger<InvoiceCacheService> logger)
    {
        _invoiceCacheRepository = invoiceCacheRepository;
        _logger = logger;
    }

    // ✅ fix 1: removed invoiceNumber parameter — Id is enough
    public async Task<InvoiceEventDto?> GetByIdAsync(Guid invoiceId)
    {
        var cache = await _invoiceCacheRepository.GetByIdAsync(invoiceId);
        return cache is null ? null : ToDto(cache);
    }

    public async Task<PagedResultDto<InvoiceEventDto>> GetByClientIdAsync(Guid clientId, int pageNumber, int pageSize)
    {
        var result = await _invoiceCacheRepository.GetByClientIdAsync(clientId, pageNumber, pageSize);
        return new PagedResultDto<InvoiceEventDto>(
            result.Items.Select(ToDto).ToList(),
            result.TotalCount,
            pageNumber,
            pageSize);
    }

    public async Task<PagedResultDto<InvoiceEventDto>> GetByStatusAsync(InvoiceStatus status, int pageNumber, int pageSize)
    {
        var result = await _invoiceCacheRepository.GetByStatusAsync(status, pageNumber, pageSize);
        return new PagedResultDto<InvoiceEventDto>(
            result.Items.Select(ToDto).ToList(),
            result.TotalCount,
            pageNumber,
            pageSize);
    }

    public async Task CreateAsync(InvoiceEventDto dto)
    {
        var invoiceCache = InvoiceCache.From(dto);
        await _invoiceCacheRepository.AddAsync(invoiceCache);
    }

    public async Task<PagedResultDto<InvoiceEventDto>> GetPagedAsync(int pageNumber, int pageSize, string? search = null)
    {
        var paged = await _invoiceCacheRepository.GetPagedAsync(pageNumber, pageSize, search);
        return new PagedResultDto<InvoiceEventDto>(
            paged.Items.Select(ToDto).ToList(),
            paged.TotalCount,
            pageNumber,
            pageSize);
    }

    // // // // // // // // // // // // // // // //
    // EVENT RELATED METHODS
    // // // // // // // // // // // // // // // //

    public async Task SyncCreatedAsync(InvoiceEventDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
        {
            _logger.LogWarning(
                "Invoice event has null or empty InvoiceNumber. Id: {InvoiceId}", dto.Id);
            return;
        }

        try
        {
            // ✅ fix 2: only pass invoiceId
            var existing = await _invoiceCacheRepository.GetByIdAsync(dto.Id);

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Invoice cache already exists for Id: {InvoiceId}, " +
                    "InvoiceNumber: {InvoiceNumber}. Skipping.",
                    dto.Id, dto.InvoiceNumber);
                return;
            }

            await _invoiceCacheRepository.AddAsync(InvoiceCache.From(dto));

            _logger.LogInformation(
                "Created invoice cache for Id: {InvoiceId}, InvoiceNumber: {InvoiceNumber}.",
                dto.Id, dto.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error syncing created invoice. Id: {InvoiceId}", dto.Id);
        }
    }

    public async Task SyncCancelledAsync(InvoiceEventDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        // Don't check status string here — the topic itself is the authority
        var existing = await _invoiceCacheRepository.GetByIdAsync(dto.Id);

        if (existing is null)
        {
            _logger.LogWarning(
                "Invoice {InvoiceId} not found in cache. Cannot cancel.", dto.Id);
            return;
        }

        existing.MarkCancelled();
        await _invoiceCacheRepository.SaveChangesAsync(existing);  // ← let it throw on failure

        _logger.LogInformation(
            "Invoice {InvoiceId} marked CANCELLED in cache.", dto.Id);
    }

    private static InvoiceEventDto ToDto(InvoiceCache cache) => new(
        Id: cache.Id,
        InvoiceNumber: cache.InvoiceNumber,
        TotalTTC: cache.TotalTTC,
        PaidAmount: cache.PaidAmount,
        RemainingAmount: cache.TotalTTC - cache.PaidAmount,
        Status: cache.Status.ToString(),
        ClientId: cache.ClientId,
        TenantId: cache.TenantId
    );
}