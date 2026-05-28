// Infrastructure/Messaging/ClientEvents/Category/ClientCategoryEventConsumer.cs
using Confluent.Kafka;
using ERP.StockService.Application.DTOs;
using System.Text.Json;

namespace ERP.StockService.Infrastructure.Messaging.Events.ClientEvents.Category;

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

        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"stock-service-client-category-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
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
        _logger.LogInformation("ClientCategoryEventConsumer started, topics: {Topics}",
            string.Join(", ", ClientCategoryTopics.Created, ClientCategoryTopics.Updated,
                ClientCategoryTopics.Deleted, ClientCategoryTopics.Restored));

        await Task.Run(async () =>
        {
            // Wait a bit for topics to be created if needed
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    // Log raw message for debugging
                    _logger.LogDebug("Raw message received on {Topic}: {Message}", result.Topic, result.Message.Value);

                    // Determine event type based on topic for logging purpose
                    string eventType = result.Topic switch
                    {
                        ClientCategoryTopics.Created => "Created",
                        ClientCategoryTopics.Updated => "Updated",
                        ClientCategoryTopics.Deleted => "Deleted",
                        ClientCategoryTopics.Restored => "Restored",
                        _ => "Unknown"
                    };

                    // For create, update, restore events, deserialize full DTO
                    ClientCategoryResponseDto? dto = JsonSerializer.Deserialize<ClientCategoryResponseDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto == null)
                    {
                        _logger.LogWarning("Failed to deserialize event on {Topic}", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    // Validate data
                    if (string.IsNullOrWhiteSpace(dto.Name))
                    {
                        _logger.LogError("Client category event has null or empty Name. EventType: {EventType}",
                            eventType);
                        _consumer.Commit(result);
                        continue;
                    }

                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IClientCategoryEventHandler handler = scope.ServiceProvider.GetRequiredService<IClientCategoryEventHandler>();

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
                        }
                    }

                    _consumer.Commit(result);
                    _logger.LogInformation("Successfully processed client category event from topic {Topic}",
                        result.Topic);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Topic not available yet: {Error}. Waiting 10 seconds...", ex.Error.Reason);
                    await Task.Delay(10000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing client category event");
                    // Don't commit the offset on error - will retry
                    await Task.Delay(1000, stoppingToken);
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