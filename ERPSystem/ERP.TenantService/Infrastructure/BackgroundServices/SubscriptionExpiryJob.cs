using ERP.TenantService.Application.Events;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Infrastructure.Messaging;

public class SubscriptionExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionExpiryJob> _logger;

    public SubscriptionExpiryJob(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CheckExpirations(ct);
            await Task.Delay(TimeSpan.FromHours(1), ct); // run every hour
        }
    }

    private async Task CheckExpirations(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var tenantRepo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var expired = await repo.GetExpiredAsync(DateTime.UtcNow, ct);

        foreach (var sub in expired)
        {
            var tenant = await tenantRepo.GetByIdAsync(sub.TenantId, ct);
            if (tenant is null) continue;

            tenant.Deactivate();
            await tenantRepo.SaveChangesAsync();

            await publisher.PublishAsync("tenant.subscription.expired",
                new SubscriptionExpiredEvent(sub.TenantId, sub.SubscriptionPlanId));

            _logger.LogWarning("Subscription expired for tenant {TenantId}", sub.TenantId);
        }
    }
}