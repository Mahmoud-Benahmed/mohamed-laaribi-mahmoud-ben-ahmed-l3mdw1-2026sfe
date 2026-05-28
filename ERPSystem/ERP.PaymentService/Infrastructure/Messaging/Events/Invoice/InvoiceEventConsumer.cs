using Confluent.Kafka;
using ERP.PaymentService.Application.DTO;
using ERP.PaymentService.Application.Interfaces.LocalCache;
using ERP.PaymentService.Application.Services;
using System.Text.Json;

namespace ERP.PaymentService.Infrastructure.Messaging.Events.Invoice;

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

            GroupId = $"payment-service-invoice-cache-v1",
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

                    _logger.LogDebug("\nRaw message received on \n{Topic}: {Message}\n\n",
                        result.Topic, result.Message.Value);

                    InvoiceEventDto? dto = JsonSerializer.Deserialize<InvoiceEventDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Null payload on {Topic}, skipping", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    _logger.LogInformation(
                        "Processing Invoice event - Topic: {Topic}, \nId: {Id}, " +
                        "\nNumber: {Number}, \nClientId: {ClientId}, " +
                        "\nStatus: {Status}, \nTotalTTC: {TotalTTC}\n\n",
                        result.Topic, dto.Id, dto.InvoiceNumber,
                        dto.ClientId, dto.Status, dto.TotalTTC);

                    if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
                    {
                        _logger.LogError("Invoice {Id} has null or empty InvoiceNumber, skipping", dto.Id);
                        _consumer.Commit(result);
                        continue;
                    }

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

                    // ✅ fix 3: removed unused IInvoiceCacheService resolution
                    IInvoiceEventHandler handler = scope.ServiceProvider
                        .GetRequiredService<IInvoiceEventHandler>();

                    switch (result.Topic)
                    {
                        case InvoiceTopics.Created:
                            await handler.HandleCreatedAsync(dto);
                            break;

                        case InvoiceTopics.Cancelled:
                            await handler.HandleCancelledAsync(dto);
                            break;

                        default:
                            // ✅ fix 4: unknown topic should be logged, not silently ignored
                            _logger.LogWarning("Unknown topic {Topic}, skipping", result.Topic);
                            break;
                    }

                    _consumer.Commit(result);

                    // ✅ fix 5: log says Invoice not Client
                    _logger.LogInformation(
                        "Successfully processed invoice {InvoiceId} from topic {Topic}",
                        dto.Id, result.Topic);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // ✅ fix 6: log says Invoice not Client
                    _logger.LogError(ex, "Error processing invoice event");
                    // offset not committed — Kafka will redeliver
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