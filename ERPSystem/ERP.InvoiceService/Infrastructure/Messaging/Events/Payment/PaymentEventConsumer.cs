using Confluent.Kafka;
using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Infrastructure.Messaging.Events.Payment;
using System.Text.Json;

public sealed class PaymentEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentEventConsumer> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PaymentEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new()
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"invoice-service-payment-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();

        // ← subscribe to both topics
        _consumer.Subscribe([PaymentTopics.InvoicePaid, PaymentTopics.Cancelled]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PaymentEventConsumer started. Topics: {Topics}",
            string.Join(", ", PaymentTopics.InvoicePaid, PaymentTopics.Cancelled));

        await Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    _logger.LogDebug("Message received — Topic: {Topic}, Payload: {Payload}",
                        result.Topic, result.Message.Value);

                    using IServiceScope scope = _scopeFactory.CreateScope();
                    IPaymentEventHandler handler = scope.ServiceProvider
                        .GetRequiredService<IPaymentEventHandler>();

                    switch (result.Topic)
                    {
                        case PaymentTopics.InvoicePaid:
                            {
                                InvoicePaidEvent? dto = JsonSerializer.Deserialize<InvoicePaidEvent>(
                                    result.Message.Value, _jsonOptions);

                                if (dto is null)
                                {
                                    _logger.LogWarning("Null InvoicePaidEvent on {Topic}, skipping.", result.Topic);
                                    break;
                                }

                                if (dto.PaidAmount <= 0)
                                {
                                    _logger.LogError(
                                        "InvoicePaidEvent has non-positive amount {Amount}. Skipping.",
                                        dto.PaidAmount);
                                    break;
                                }

                                _logger.LogInformation(
                                    "Processing InvoicePaidEvent — PaymentId: {PaymentId}, " +
                                    "InvoiceId: {InvoiceId}, PaidAmount: {PaidAmount}",
                                    dto.PaymentId, dto.InvoiceId, dto.PaidAmount);

                                await handler.HandleInvoicePaidAsync(dto);

                                _logger.LogInformation(
                                    "Processed InvoicePaidEvent — PaymentId: {PaymentId}",
                                    dto.PaymentId);
                                break;
                            }

                        case PaymentTopics.Cancelled:
                            {
                                PaymentCancelledEvent? dto = JsonSerializer.Deserialize<PaymentCancelledEvent>(
                                    result.Message.Value, _jsonOptions);

                                if (dto is null)
                                {
                                    _logger.LogWarning("Null PaymentCancelledEvent on {Topic}, skipping.", result.Topic);
                                    break;
                                }

                                _logger.LogInformation(
                                    "Processing PaymentCancelledEvent — PaymentId: {PaymentId}, " +
                                    "InvoiceId: {InvoiceId}, ReversedAmount: {Amount}",
                                    dto.PaymentId, dto.InvoiceId, dto.ReversedAmount);

                                await handler.HandlePaymentCancelledAsync(dto);

                                _logger.LogInformation(
                                    "Processed PaymentCancelledEvent — PaymentId: {PaymentId}",
                                    dto.PaymentId);
                                break;
                            }

                        default:
                            _logger.LogWarning("Unknown topic {Topic}, skipping.", result.Topic);
                            break;
                    }

                    // Commit only after successful handling
                    _consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Don't commit — message will be redelivered
                    _logger.LogError(ex, "Error processing payment event. Offset not committed.");
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