using Confluent.Kafka;
using ERP.ArticleService.Application.Services;
using System.Text.Json;

namespace ERP.ArticleService.Infrastructure.Messaging;

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

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "article-tenant-consumer-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();

        _consumer.Subscribe(new[] { TenantTopics.TenantCreated });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tenant Kafka consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;

            try
            {
                result = _consumer.Consume(stoppingToken);

                if (result?.Message?.Value == null)
                    continue;

                var evt = JsonSerializer.Deserialize<TenantCreatedEvent>(
                    result.Message.Value,
                    _jsonOptions);

                if (evt == null)
                    continue;

                using var scope = _scopeFactory.CreateScope();
                var services = scope.ServiceProvider;


                var provisioner = services.GetRequiredService<ITenantProvisioningService>();

                await provisioner.ProvisionAsync(evt.TenantId, evt.Slug);


                _logger.LogInformation(
                    "Tenant provisioned: {TenantId} / {Slug}",
                    evt.TenantId, evt.Slug
                );

                _consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tenant.created");
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