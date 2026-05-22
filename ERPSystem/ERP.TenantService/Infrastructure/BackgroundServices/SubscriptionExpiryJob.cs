using ERP.TenantService.Application.Events;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;

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
        int totalDelay = 0;
        int checkInterval = 15;
        string delayUnit = "minutes";

        while (!ct.IsCancellationRequested)
        {
            await CheckExpirations(ct);
            await Task.Delay(TimeSpan.FromMinutes(checkInterval), ct); // run every 15 minutes (default)
            _logger.LogInformation($"Check done after: {totalDelay+= checkInterval} {delayUnit}");
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
            try
            {
                var tenant = await tenantRepo.GetByIdAsync(sub.TenantId, ct);
                if (tenant is null)
                    continue;

                if (!tenant.IsActive)
                    continue;

                await tenantRepo.SaveChangesAsync();

                await publisher.PublishAsync(TenantTopics.TenantSuspended,
                    new TenantSuspendedEvent(tenant.Id, tenant.Slug));

                await publisher.PublishAsync(TenantTopics.SubscriptionExpired,
                    new SubscriptionExpiredEvent(sub.TenantId, sub.SubscriptionPlanId));


                _logger.LogWarning("Subscription expired for tenant {TenantId}", sub.TenantId);
            }
            catch (Exception ex)
            {
                // Log and continue — this tenant will be retried on the next hourly run
                _logger.LogError(ex,
                    "Failed to process expired subscription for tenant {TenantId}",
                    sub.TenantId);
            }
        }
    }
}