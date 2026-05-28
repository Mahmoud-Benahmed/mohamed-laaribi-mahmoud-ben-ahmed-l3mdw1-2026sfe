using Confluent.Kafka;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Application.Services;
using ERP.InvoiceService.Domain.LocalCache.Tenant;
using ERP.InvoiceService.Infrastructure.Persistence;
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;
using System.Text.Json;

namespace ERP.InvoiceService.Infrastructure.Messaging.Events.TenantEvent;

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
            GroupId = $"invoice-service-tenant-consumer-v1",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();

        _consumer.Subscribe([TenantTopics.TenantCreated, TenantTopics.TenantUpdated, TenantTopics.TenantDeleted]);
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

                await (result.Topic switch
                {
                    TenantTopics.TenantCreated => HandleTenantCreated(result.Message.Value, scope),
                    TenantTopics.TenantUpdated => HandleTenantUpdated(result.Message.Value, scope),
                    TenantTopics.TenantDeleted => HandleTenantDeleted(result.Message.Value, scope),
                    _ => Task.CompletedTask
                });

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
    private async Task HandleTenantCreated(string payload, IServiceScope scope)
    {
        var evt = JsonSerializer.Deserialize<TenantCreatedEvent>(payload, _jsonOptions);
        if (evt is null) return;

        var repo = scope.ServiceProvider.GetRequiredService<ITenantCacheRepository>();
        await repo.AddAsync(TenantCache.Create(evt));
        await repo.SaveChangesAsync();

        var invoiceNumberRepo = scope.ServiceProvider.GetRequiredService<IInvoiceNumberGenerator>();
        await invoiceNumberRepo.GenerateNextInvoiceNumberAsync(evt.TenantId);

        _logger.LogInformation("Tenant created: {TenantId}", evt.TenantId);
    }

    private async Task HandleTenantUpdated(string payload, IServiceScope scope)
    {
        var evt = JsonSerializer.Deserialize<TenantUpdatedEvent>(payload, _jsonOptions);
        if (evt is null) return;

        var repo = scope.ServiceProvider.GetRequiredService<ITenantCacheRepository>();
        var tenant = await repo.GetByIdAsync(evt.TenantId);
        if (tenant is null) return;

        tenant.Update(evt);
        await repo.SaveChangesAsync();

        _logger.LogInformation("Tenant updated: {TenantId}", evt.TenantId);
    }
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