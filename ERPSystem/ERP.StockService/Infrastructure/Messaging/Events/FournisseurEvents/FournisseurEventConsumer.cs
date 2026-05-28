using Confluent.Kafka;
using ERP.StockService.Application.DTOs;
using System.Text.Json;

namespace ERP.StockService.Infrastructure.Messaging.Events.FournisseurEvents;

public sealed class FournisseurEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FournisseurEventConsumer> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FournisseurEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<FournisseurEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"stock-service-fournisseur-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true  // Add this
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([FournisseurTopics.Created, FournisseurTopics.Updated, FournisseurTopics.Deleted, FournisseurTopics.Restored]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FournisseurEventConsumer started, topics: {Topics}",
            string.Join(", ", FournisseurTopics.Created, FournisseurTopics.Updated, FournisseurTopics.Deleted, FournisseurTopics.Restored));

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

                    FournisseurResponseDto? dto = JsonSerializer.Deserialize<FournisseurResponseDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Null payload on {Topic}, skipping", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    // FIXED: Log client data, not article data
                    _logger.LogInformation("Processing fournisseur: Id={Id}, Name={Name}, Email={Email}",
                        dto.Id, dto.Name, dto.Email);

                    // FIXED: Client doesn't have Category - remove category validation
                    // Just validate basic client data
                    if (string.IsNullOrWhiteSpace(dto.Name))
                    {
                        _logger.LogError("Fournisseur {FounisseurId} has null or empty Name", dto.Id);
                        _consumer.Commit(result);
                        continue;
                    }

                    // Create a new scope for each message
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IFournisseurEventHandler handler = scope.ServiceProvider.GetRequiredService<IFournisseurEventHandler>();

                        switch (result.Topic)
                        {
                            case FournisseurTopics.Created:
                                await handler.HandleCreatedAsync(dto);
                                break;
                            case FournisseurTopics.Updated:
                                await handler.HandleUpdatedAsync(dto);
                                break;
                            case FournisseurTopics.Deleted:
                                await handler.HandleDeletedAsync(dto);
                                break;
                            case FournisseurTopics.Restored:
                                await handler.HandleRestoredAsync(dto);
                                break;
                        }
                    }

                    _consumer.Commit(result);
                    _logger.LogInformation("Successfully processed fournisseur {FournisseurId} from topic {Topic}",
                        dto.Id, result.Topic);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing fournisseur event");
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