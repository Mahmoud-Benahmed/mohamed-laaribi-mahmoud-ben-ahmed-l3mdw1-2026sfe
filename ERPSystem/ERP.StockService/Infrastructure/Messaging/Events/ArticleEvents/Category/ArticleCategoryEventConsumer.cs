using Confluent.Kafka;
using ERP.StockService.Application.DTOs;
using System.Text.Json;

namespace ERP.StockService.Infrastructure.Messaging.Events.ArticleEvents.Category;

public sealed class ArticleCategoryEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArticleCategoryEventConsumer> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ArticleCategoryEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<ArticleCategoryEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"stock-service-article-category-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true,
            SocketTimeoutMs = 60000,
            SessionTimeoutMs = 60000
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([ArticleCategoryTopics.Created, ArticleCategoryTopics.Updated, ArticleCategoryTopics.Deleted, ArticleCategoryTopics.Restored]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    // Log raw message
                    _logger.LogInformation("Raw category message: {Raw}", result.Message.Value);

                    ArticleCategoryResponseDto? dto = JsonSerializer.Deserialize<ArticleCategoryResponseDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Failed to deserialize category event");
                        _consumer.Commit(result);
                        continue;
                    }

                    _logger.LogInformation("Deserialized category - Id: {Id}, Name: '{Name}', TVA: {TVA}",
                        dto.Id, dto.Name ?? "NULL", dto.TVA);

                    if (string.IsNullOrWhiteSpace(dto.Name))
                    {
                        _logger.LogError("Category name is null or empty! Raw JSON: {Raw}", result.Message.Value);
                        _consumer.Commit(result);
                        continue;
                    }

                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IArticleCategoryEventHandler handler = scope.ServiceProvider.GetRequiredService<IArticleCategoryEventHandler>();

                        switch (result.Topic)
                        {
                            case ArticleCategoryTopics.Created:
                                await handler.HandleCreatedAsync(dto);
                                break;
                            case ArticleCategoryTopics.Updated:
                                await handler.HandleUpdatedAsync(dto);
                                break;
                            case ArticleCategoryTopics.Deleted:
                                await handler.HandleDeletedAsync(dto);
                                break;
                            case ArticleCategoryTopics.Restored:
                                await handler.HandleRestoredAsync(dto);
                                break;
                        }
                    }

                    _consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Topic not available: {Error}. Waiting...", ex.Error.Reason);
                    await Task.Delay(10000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing category event");
                    await Task.Delay(1000, stoppingToken); // backoff, then continue implicitly
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