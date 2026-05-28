using Confluent.Kafka;
using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Services;
using System.Text.Json;

namespace ERP.StockService.Infrastructure.Messaging.Events.InvoiceEvents;

public sealed class InvoiceEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceEventConsumer> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InvoiceEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"stock-service-invoice-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([InvoiceTopics.Created, InvoiceTopics.Cancelled]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InvoiceEventConsumer started, topics: {Topics}",
            string.Join(", ", InvoiceTopics.Created, InvoiceTopics.Cancelled));

        await Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    _logger.LogDebug("Raw message received on {Topic}: {Message}",
                        result.Topic, result.Message.Value);

                    InvoiceDto? dto = JsonSerializer.Deserialize<InvoiceDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Null payload on {Topic}, skipping.", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    if (!dto.TenantId.HasValue)
                    {
                        _logger.LogError(
                            "Missing TenantId for article event {ArticleId}",
                            dto.Id);

                        return;
                    }

                    using IServiceScope scope = _scopeFactory.CreateScope();
                    var tenantContext =
                        scope.ServiceProvider.GetRequiredService<ITenantContext>();

                    tenantContext.SetTenantId(dto.TenantId.Value);


                    IInvoiceEventHandler handler = scope.ServiceProvider.GetRequiredService<IInvoiceEventHandler>();

                    switch (result.Topic)
                    {
                        case InvoiceTopics.Created:
                            await handler.HandleCreatedAsync(dto);
                            break;
                        case InvoiceTopics.Cancelled:
                            await handler.HandleCancelledAsync(dto);
                            break;
                        default:
                            _logger.LogWarning("Unhandled invoice topic: {Topic}", result.Topic);
                            break;
                    }

                    _consumer.Commit(result);
                    _logger.LogInformation("Processed invoice {InvoiceId} from topic {Topic}",
                        dto.Id, result.Topic);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing invoice event.");
                }
            }

            _consumer.Close();
        }, stoppingToken);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}


/*
 
STOPPED HERE
TODO/
* IMPLEMENT IInvoiceEventHandler.HandleCreatedAsync(InvoiceDto dto) 
    to create a journal entry for the invoice  
    && integrate the logic in the BonSortieService to update the stock and journal when a bon de sortie is created for an invoice

* IMPLEMENT IInvoiceEventHandler.HandleCancelledAsync(InvoiceDto dto) 
    to reverse the journal entry for the cancelled invoice 
    && integrate the logic in the BonSortieService to reverse the stock and journal when a bon de sortie is cancelled for an invoice
 
NEXT STEP: InvoiceEventHandler to implement the logic for handling created and cancelled invoice events, and then update the BonSortieService to interact with the journal and stock accordingly when a bon de sortie is created or cancelled for an invoice.
 */