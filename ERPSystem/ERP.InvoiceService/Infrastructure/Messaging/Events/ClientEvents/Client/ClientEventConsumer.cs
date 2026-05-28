using Confluent.Kafka;
using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Infrastructure.Messaging.Events.ClientEvents.Client;
using System.Text.Json;

public sealed class ClientEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClientEventConsumer> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ClientEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<ClientEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new()
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"invoice-service-client-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([
            ClientTopics.Created,
            ClientTopics.Updated,
            ClientTopics.Deleted,
            ClientTopics.Restored
        ]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClientEventConsumer started. Topics: {Topics}",
            string.Join(", ", ClientTopics.Created, ClientTopics.Updated,
                              ClientTopics.Deleted, ClientTopics.Restored));

        await Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    _logger.LogDebug("Message received — Topic: {Topic}, Payload: {Payload}",
                        result.Topic, result.Message.Value);

                    ClientResponseDto? dto = JsonSerializer.Deserialize<ClientResponseDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Null payload on {Topic}, skipping.", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(dto.Name))
                    {
                        _logger.LogError(
                            "Client {ClientId} has null or empty Name on topic {Topic}. Skipping.",
                            dto.Id, result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    _logger.LogInformation(
                        "Processing {Topic} — ClientId: {ClientId}, Name: {Name}, Email: {Email}",
                        result.Topic, dto.Id, dto.Name, dto.Email);

                    using IServiceScope scope = _scopeFactory.CreateScope();
                    IClientEventHandler handler = scope.ServiceProvider
                        .GetRequiredService<IClientEventHandler>(); // ← removed unused IClientCacheService

                    switch (result.Topic)
                    {
                        case ClientTopics.Created:
                            await handler.HandleCreatedAsync(dto);
                            break;
                        case ClientTopics.Updated:
                            await handler.HandleUpdatedAsync(dto);
                            break;
                        case ClientTopics.Deleted:
                            await handler.HandleDeletedAsync(dto);
                            break;
                        case ClientTopics.Restored:
                            await handler.HandleRestoredAsync(dto);
                            break;
                        default:
                            _logger.LogWarning("Unknown topic {Topic}, skipping.", result.Topic);
                            break;
                    }

                    // Commit only after successful handling
                    _consumer.Commit(result);

                    _logger.LogInformation(
                        "Processed {Topic} — ClientId: {ClientId}",
                        result.Topic, dto.Id);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Don't commit — message will be redelivered
                    _logger.LogError(ex, "Error processing client event. Offset not committed.");
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