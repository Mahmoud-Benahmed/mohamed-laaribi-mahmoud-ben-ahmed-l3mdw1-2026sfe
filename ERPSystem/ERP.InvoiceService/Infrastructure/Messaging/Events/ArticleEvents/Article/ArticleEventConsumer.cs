using Confluent.Kafka;
using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using System.Text.Json;

namespace ERP.InvoiceService.Infrastructure.Messaging.Events.ArticleEvents.Article;

public sealed class ArticleEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArticleEventConsumer> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ArticleEventConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<ArticleEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            GroupId = $"invoice-service-article-cache-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe([ArticleTopics.Created, ArticleTopics.Updated, ArticleTopics.Deleted, ArticleTopics.Restored]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ArticleEventConsumer started, topics: {Topics}",
            string.Join(", ", [ArticleTopics.Created, ArticleTopics.Updated, ArticleTopics.Deleted, ArticleTopics.Restored]));

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

                    ArticleResponseDto? dto = JsonSerializer.Deserialize<ArticleResponseDto>(
                        result.Message.Value, _jsonOptions);

                    if (dto is null)
                    {
                        _logger.LogWarning("Null payload on {Topic}, skipping", result.Topic);
                        _consumer.Commit(result);
                        continue;
                    }

                    // Log deserialized data
                    _logger.LogInformation("Processing article: {dto}", dto);

                    // Validate category data
                    if (dto.Category == null)
                    {
                        _logger.LogError("Article {ArticleId} has null Category", dto.Id);
                        _consumer.Commit(result);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(dto.Category.Name))
                    {
                        _logger.LogError("Article {ArticleId} has category with null/empty Name. Category Id: {CategoryId}",
                            dto.Id, dto.Category.Id);
                        _consumer.Commit(result);
                        continue;
                    }

                    // Create a new scope for each message
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IArticleCategoryCacheService categoryCacheService = scope.ServiceProvider.GetRequiredService<IArticleCategoryCacheService>();

                        // Check if category exists (using async properly)
                        bool categoryExists = await categoryCacheService.ExistsAsync(dto.Category.Name) || await categoryCacheService.GetByIdAsync(dto.Category.Id) != null;

                        if (!categoryExists)
                        {
                            _logger.LogWarning("Category {CategoryId} ({CategoryName}) not found in cache for article {ArticleId}. " +
                                                "Creating category first.",
                                                dto.Category.Id, dto.Category.Name, dto.Id);

                            await categoryCacheService.SyncCreatedAsync(dto.Category);
                        }

                        IArticleEventHandler handler = scope.ServiceProvider.GetRequiredService<IArticleEventHandler>();

                        switch (result.Topic)
                        {
                            case ArticleTopics.Created:
                                await handler.HandleCreatedAsync(dto);
                                break;
                            case ArticleTopics.Updated:
                                await handler.HandleUpdatedAsync(dto);
                                break;
                            case ArticleTopics.Deleted:
                                await handler.HandleDeletedAsync(dto);
                                break;
                            case ArticleTopics.Restored:
                                await handler.HandleRestoredAsync(dto);
                                break;
                        }
                    }

                    _consumer.Commit(result);
                    _logger.LogInformation("Successfully processed article {ArticleId} from topic {Topic}",
                        dto.Id, result.Topic);
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
                    _logger.LogError(ex, "Error processing article event");
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