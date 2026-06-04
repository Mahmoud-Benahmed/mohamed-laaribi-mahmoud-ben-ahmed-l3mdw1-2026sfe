using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Client;
using ERP.InvoiceService.Infrastructure.Messaging.Events;
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;

public sealed class OverdueInvoiceJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueInvoiceJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    public OverdueInvoiceJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueInvoiceJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private async Task ProcessOverdueInvoicesAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        IInvoiceRepository invoiceRepo = scope.ServiceProvider
            .GetRequiredService<IInvoiceRepository>();

        IClientCacheRepository clientRepo = scope.ServiceProvider
            .GetRequiredService<IClientCacheRepository>();

        IInvoiceNumberGenerator numberGenerator = scope.ServiceProvider
            .GetRequiredService<IInvoiceNumberGenerator>();

        ITenantCacheRepository tenantRepo = scope.ServiceProvider  // ✅ resolve from scope
            .GetRequiredService<ITenantCacheRepository>();

        IEventPublisher _eventPublisher = scope.ServiceProvider  // ✅ resolve from scope
                    .GetRequiredService<IEventPublisher>();


        DateTime now = DateTime.UtcNow;
        List<Invoice> overdueInvoices = await invoiceRepo.GetOverdueAsync(now);

        if (!overdueInvoices.Any())
        {
            _logger.LogInformation("No overdue invoices found at {Time}.", now);
            return;
        }

        // Group by tenant — process each tenant's invoices with correct tenantId
        foreach (var group in overdueInvoices.GroupBy(i => i.TenantId))
        {
            Guid? tenantId = group.Key;

            foreach (Invoice invoice in group)
            {
                try
                {
                    ClientCache? client = await clientRepo.GetByIdAsync(invoice.ClientId);
                    if (client is null) continue;

                    int duePeriod = client.GetEffectiveDuePaymentPeriod();
                    if (now < invoice.InvoiceDate.AddDays(duePeriod)) continue;

                    bool penaltyExistsForPeriod = await invoiceRepo
                        .PenaltyExistsForPeriodAsync(invoice.OriginalInvoiceNumber ?? invoice.InvoiceNumber,
                                                      DateTime.UtcNow,
                                                      duePeriod);
                    if (penaltyExistsForPeriod) continue;

                    string penaltyNumber = await numberGenerator
                        .GenerateNextInvoiceNumberAsync(tenantId); // ✅ from invoice, not HTTP context

                    Invoice penaltyInvoice = invoice.CreatePenaltyInvoice(
                        invoiceNumber: penaltyNumber, 
                        duePeriod: duePeriod, 
                        tenantId: tenantId);
                    await invoiceRepo.AddAsync(penaltyInvoice);

                    InvoiceEventDto payload = new InvoiceEventDto(
                        Id: penaltyInvoice.Id,
                        InvoiceNumber: penaltyInvoice.InvoiceNumber,
                        TotalTTC: penaltyInvoice.TotalTTC,
                        Status: penaltyInvoice.Status.ToString(),
                        ClientId: penaltyInvoice.ClientId,
                        TenantId: penaltyInvoice.TenantId,
                        Items: penaltyInvoice.Items.Select(i => new InvoiceItemEventDto(
                            ArticleId: i.ArticleId,
                            Quantity: i.Quantity,
                            UniPriceHT: i.UniPriceHT,
                            TaxRate: i.TaxRate
                        )).ToList()
                    );
                    await _eventPublisher.PublishAsync(InvoiceTopics.Created, payload);

                    _logger.LogInformation(
                        "Penalty invoice {PenaltyNumber} created for {InvoiceNumber}",
                        penaltyNumber, invoice.InvoiceNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create penalty for invoice {Id}", invoice.Id);
                }
            }
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueInvoiceJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Checking for UNPAID invoices...");
                await ProcessOverdueInvoicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing overdue invoices.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}