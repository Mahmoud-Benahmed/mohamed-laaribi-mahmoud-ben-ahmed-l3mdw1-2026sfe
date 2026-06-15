using Confluent.Kafka;
using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Application.Services;
using System.Text.Json;

namespace ERP.StockService.Infrastructure.Messaging.Events.ClientEvents.Client;

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

        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"stock-service-client-category-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true  // Add this
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([ClientTopics.Created, ClientTopics.Updated, ClientTopics.Deleted, ClientTopics.Restored]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClientEventConsumer started, topics: {Topics}",
            string.Join(", ", ClientTopics.Created, ClientTopics.Updated, ClientTopics.Deleted, ClientTopics.Restored));

        await Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    // Log raw message for debugging
                    _logger.LogDebug("Raw message received on {Topic}: {Message}",
                        result.Topic, result.Message.Value);

                    ClientResponseDto? dto = JsonSerializer.Deserialize<ClientResponseDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Null payload on {Topic}, skipping", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    // FIXED: Log client data, not article data
                    _logger.LogInformation("Processing client: Id={Id}, Name={Name}, Email={Email}",
                        dto.Id, dto.Name, dto.Email);

                    // FIXED: Client doesn't have Category - remove category validation
                    // Just validate basic client data
                    if (string.IsNullOrWhiteSpace(dto.Name))
                    {
                        _logger.LogError("Client {ClientId} has null or empty Name", dto.Id);
                        _consumer.Commit(result);
                        continue;
                    }


                    _logger.LogWarning($"Payload recived: {dto}");

                    // Create a new scope for each message
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        if (!dto.TenantId.HasValue)
                        {
                            _logger.LogError(
                                "Missing TenantId for article event {ArticleId}",
                                dto.Id);

                            return;
                        }

                        var tenantContext =
                            scope.ServiceProvider.GetRequiredService<ITenantContext>();

                        tenantContext.SetTenantId(dto.TenantId.Value);

                        IClientEventHandler handler = scope.ServiceProvider.GetRequiredService<IClientEventHandler>();

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
                        }
                    }

                    _consumer.Commit(result);
                    _logger.LogInformation("Successfully processed client {ClientId} from topic {Topic}",
                        dto.Id, result.Topic);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing client event");
                    // Don't commit the offset on error - will retry
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