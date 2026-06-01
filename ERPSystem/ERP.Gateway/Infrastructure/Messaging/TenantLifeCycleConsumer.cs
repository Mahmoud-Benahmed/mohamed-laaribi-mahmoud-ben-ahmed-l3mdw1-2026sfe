using Confluent.Kafka;
using ERP.Gateway.Cache;
using System.Text.Json;

namespace ERP.Gateway.Infrastructure.Messaging;

public sealed class TenantLifecycleConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantLifecycleConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TenantLifecycleConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<TenantLifecycleConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        ConsumerConfig config = new()
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "gateway-tenant-lifecycle-consumer-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();

        _consumer.Subscribe(new[]
        {
            TenantTopics.TenantCreated,
            TenantTopics.TenantUpdated,
            TenantTopics.TenantSuspended,
            TenantTopics.TenantActivated,
            TenantTopics.TenantDeleted,
            TenantTopics.TenantRestored
        });
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ConsumeResult<string, string>? result = _consumer.Consume(stoppingToken);

                if (result?.Message?.Value == null)
                    continue;

                using IServiceScope scope = _scopeFactory.CreateScope();

                ITenantCache cache =
                    scope.ServiceProvider
                        .GetRequiredService<ITenantCache>();

                string topic = result.Topic;

                var json = result.Message.Value;

                switch (topic)
                {
                    case TenantTopics.TenantCreated:
                        {
                            TenantCreatedEvent? evt = JsonSerializer.Deserialize<TenantCreatedEvent>(json, _jsonOptions);

                            if (evt == null)
                                continue;

                            await cache.SetAsync(new TenantCacheEntry
                            {
                                TenantId = evt.TenantId,
                                Slug = evt.Slug,
                                IsActive = evt.IsActive
                            });

                            _logger.LogInformation("Tenant created cached: {Slug}", evt.Slug);

                            break;
                        }

                    case TenantTopics.TenantUpdated:
                        {
                            TenantUpdatedEvent? evt =
                                JsonSerializer.Deserialize<TenantUpdatedEvent>(json, _jsonOptions);

                            if (evt == null)
                                continue;

                            // remove old slug if changed
                            if (!string.Equals(
                                    evt.OldSlug,
                                    evt.NewSlug,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                await cache.RemoveAsync(evt.OldSlug);
                            }

                            await cache.SetAsync(new TenantCacheEntry
                            {
                                TenantId = evt.TenantId,
                                Slug = evt.NewSlug,
                                IsActive = evt.IsActive
                            });

                            _logger.LogInformation(
                                "Tenant updated cached: {Slug}",
                                evt.NewSlug);

                            break;
                        }

                    case TenantTopics.TenantSuspended:
                        {
                            TenantSuspendedEvent? evt =
                                JsonSerializer.Deserialize<TenantSuspendedEvent>(json, _jsonOptions);

                            if (evt == null)
                                continue;

                            TenantCacheEntry? existing =
                                await cache.GetAsync(evt.Slug);

                            if (existing != null)
                            {
                                existing.IsActive = false;

                                await cache.SetAsync(existing);
                            }

                            _logger.LogWarning(
                                "Tenant suspended: {Slug}",
                                evt.Slug);

                            break;
                        }

                    case TenantTopics.TenantActivated:
                        {
                            TenantActivatedEvent? evt =
                                JsonSerializer.Deserialize<TenantActivatedEvent>(json, _jsonOptions);

                            if (evt == null)
                                continue;

                            TenantCacheEntry? existing =
                                await cache.GetAsync(evt.Slug);

                            if (existing != null)
                            {
                                existing.IsActive = true;

                                await cache.SetAsync(existing);
                            }

                            _logger.LogInformation(
                                "Tenant activated: {Slug}",
                                evt.Slug);

                            break;
                        }

                    case TenantTopics.TenantDeleted:
                        {
                            TenantDeletedEvent? evt =
                                JsonSerializer.Deserialize<TenantDeletedEvent>(json, _jsonOptions);

                            if (evt == null)
                                continue;

                            await cache.RemoveAsync(evt.Slug);

                            _logger.LogWarning(
                                "Tenant removed from cache: {Slug}",
                                evt.Slug);

                            break;
                        }

                    case TenantTopics.TenantRestored:
                        {
                            TenantRestoredEvent? evt =
                                JsonSerializer.Deserialize<TenantRestoredEvent>(json, _jsonOptions);

                            if (evt == null)
                                continue;

                            await cache.SetAsync(new TenantCacheEntry
                            {
                                TenantId = evt.TenantId,
                                Slug = evt.Slug,
                                IsActive = evt.IsActive
                            });

                            _logger.LogWarning(
                                "Tenant restored in cache: {Slug}",
                                evt.Slug);

                            break;
                        }

                    default:
                        {
                            _logger.LogWarning(
                                "Unhandled tenant topic: {Topic}",
                                topic);

                            break;
                        }
                }
                _consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(
                    ex,
                    "Kafka consume error");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled tenant lifecycle consumer error");
            }
        }
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();

        base.Dispose();
    }
}