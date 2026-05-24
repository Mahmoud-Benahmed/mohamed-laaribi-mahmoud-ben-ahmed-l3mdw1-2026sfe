using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Client;
using ERP.InvoiceService.Infrastructure.Persistence;
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;

namespace ERP.InvoiceService.Infrastructure.BackgroundServices;

public sealed class OverdueInvoiceJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueInvoiceJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public OverdueInvoiceJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueInvoiceJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueInvoiceJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOverdueInvoicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing overdue invoices.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
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

        DateTime now = DateTime.UtcNow;

        // Fetch only UNPAID invoices past their due date
        List<Invoice> overdueInvoices = await invoiceRepo.GetOverdueAsync(now);

        if (!overdueInvoices.Any())
        {
            _logger.LogInformation("No overdue invoices found at {Time}.", now);
            return;
        }

        _logger.LogInformation(
            "Found {Count} overdue invoices to process.",
            overdueInvoices.Count);

        foreach (Invoice invoice in overdueInvoices)
        {
            try
            {
                ClientCache? client = await clientRepo.GetByIdAsync(invoice.ClientId);

                if (client is null)
                {
                    _logger.LogWarning(
                        "Client {ClientId} not found for invoice {InvoiceId}. Skipping.",
                        invoice.ClientId, invoice.Id);
                    continue;
                }

                int duePeriod = client.GetEffectiveDuePaymentPeriod();

                DateTime expectedDueDate = invoice.InvoiceDate.AddDays(duePeriod);

                if (now < expectedDueDate)
                {
                    _logger.LogDebug(
                        "Invoice {InvoiceId} not yet past effective due date {DueDate}. Skipping.",
                        invoice.Id, expectedDueDate);
                    continue;
                }

                // Avoid duplicate penalty invoices
                bool penaltyExists = await invoiceRepo.PenaltyExistsForInvoiceAsync(invoice.InvoiceNumber);

                if (penaltyExists)
                {
                    _logger.LogDebug(
                        "Penalty invoice already exists for {InvoiceNumber}. Skipping.",
                        invoice.InvoiceNumber);
                    continue;
                }

                string penaltyNumber = await numberGenerator.GenerateNextInvoiceNumberAsync();
                Invoice penaltyInvoice = invoice.CreatePenaltyInvoice(penaltyNumber);

                await invoiceRepo.AddAsync(penaltyInvoice);

                _logger.LogInformation(
                    "Penalty invoice {PenaltyNumber} created for overdue invoice {InvoiceNumber} (client {ClientId}).",
                    penaltyNumber, invoice.InvoiceNumber, invoice.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create penalty invoice for invoice {InvoiceId}.",
                    invoice.Id);
            }
        }
    }
}