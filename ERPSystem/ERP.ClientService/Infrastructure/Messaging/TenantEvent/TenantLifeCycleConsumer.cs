using Confluent.Kafka;
using ERP.ClientService.Application.Services;
using System.Text.Json;

namespace ERP.ClientService.Infrastructure.Messaging;

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
            GroupId = $"client-service-tenant-consumer-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();

        _consumer.Subscribe([TenantTopics.TenantDeleted]);
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

                using var scope = _scopeFactory.CreateScope();

                if (result.Topic == TenantTopics.TenantDeleted)
                {
                    await HandleTenantDeleted(result.Message.Value, scope);
                }

                _consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing message from topic '{Topic}'",
                    result?.Topic ?? "unknown");
            }
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private async Task HandleTenantDeleted(string payload, IServiceScope scope)
    {
        var evt = JsonSerializer.Deserialize<TenantDeletedEvent>(payload, _jsonOptions);
        if (evt is null) return;

        var provisioner = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
        await provisioner.DeleteAllByTenantIdAsync(evt.TenantId);

        _logger.LogInformation(
            "Tenant deleted: {TenantId}",
            evt.TenantId);
    }
    // ── Dispose ───────────────────────────────────────────────────────────────

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}