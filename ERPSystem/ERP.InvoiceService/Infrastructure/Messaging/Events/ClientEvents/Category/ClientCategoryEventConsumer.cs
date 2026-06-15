using Confluent.Kafka;
using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Services;
using ERP.InvoiceService.Infrastructure.Messaging.Events.ClientEvents.Category;
using System.Text.Json;

public sealed class ClientCategoryEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClientCategoryEventConsumer> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ClientCategoryEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<ClientCategoryEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new()
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"invoice-service-client-category-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true,
            SocketTimeoutMs = 60000,
            SessionTimeoutMs = 60000
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([
            ClientCategoryTopics.Created,
            ClientCategoryTopics.Updated,
            ClientCategoryTopics.Deleted,
            ClientCategoryTopics.Restored
        ]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClientCategoryEventConsumer started. Topics: {Topics}",
            string.Join(", ", ClientCategoryTopics.Created, ClientCategoryTopics.Updated,
                              ClientCategoryTopics.Deleted, ClientCategoryTopics.Restored));

        await Task.Run(async () =>
        {
            // ← removed Task.Delay(5000) — AllowAutoCreateTopics handles missing topics,
            //   and ConsumeException below handles transient unavailability

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    _logger.LogDebug("Message received — Topic: {Topic}, Payload: {Payload}",
                        result.Topic, result.Message.Value);

                    ClientCategoryResponseDto? dto = JsonSerializer.Deserialize<ClientCategoryResponseDto>(
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
                            "ClientCategoryEvent on {Topic} has null or empty Name. ClientCategoryId: {Id}. Skipping.",
                            result.Topic, dto.Id);
                        _consumer.Commit(result);
                        continue;
                    }

                    _logger.LogInformation(
                        "Processing {Topic} — CategoryId: {Id}, Name: {Name}",
                        result.Topic, dto.Id, dto.Name);

                    using IServiceScope scope = _scopeFactory.CreateScope();

                    var tenantContext =
                        scope.ServiceProvider.GetRequiredService<ITenantContext>();

                    tenantContext.SetTenantId(dto.TenantId.Value);

                    IClientCategoryEventHandler handler = scope.ServiceProvider
                        .GetRequiredService<IClientCategoryEventHandler>();

                    switch (result.Topic)
                    {
                        case ClientCategoryTopics.Created:
                            await handler.HandleCreatedAsync(dto);
                            break;
                        case ClientCategoryTopics.Updated:
                            await handler.HandleUpdatedAsync(dto);
                            break;
                        case ClientCategoryTopics.Deleted:
                            await handler.HandleDeletedAsync(dto);
                            break;
                        case ClientCategoryTopics.Restored:
                            await handler.HandleRestoredAsync(dto);
                            break;
                        default:
                            _logger.LogWarning("Unknown topic {Topic}, skipping.", result.Topic);
                            break;
                    }

                    // Commit only after successful handling
                    _consumer.Commit(result);

                    _logger.LogInformation(
                        "Processed {Topic} — CategoryId: {Id}",
                        result.Topic, dto.Id);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Transient broker issue — log and wait before retrying
                    _logger.LogWarning(
                        "ConsumeException on topic — Reason: {Reason}. Retrying in 5s.",
                        ex.Error.Reason);
                    await Task.Delay(5000, stoppingToken);
                }
                catch (Exception ex)
                {
                    // Don't commit — message will be redelivered
                    _logger.LogError(ex, "Error processing client category event. Offset not committed.");
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