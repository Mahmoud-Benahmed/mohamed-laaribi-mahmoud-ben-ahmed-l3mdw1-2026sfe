using ERP.TenantService.Application.DTOs.Events;
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
        const DelayUnit delayUnit = DelayUnit.Minute;
        TimeSpan delay = ToTimeSpan(delayUnit);
        TimeSpan totalElapsed = TimeSpan.Zero;

        while (!ct.IsCancellationRequested)
        {
            await CheckExpirations(ct);
            await Task.Delay(delay, ct);
            totalElapsed += delay;
            _logger.LogInformation(
                "Check done — total elapsed: {Elapsed}",
                totalElapsed);
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

                if (tenant is null || !tenant.IsActive)
                    continue;

                tenant.RemoveSubscription();
                tenant.Suspend();

                // 2. Persist — single save
                await repo.DeleteByTenantIdAsync(sub.TenantId, ct);
                await tenantRepo.UpdateAsync(tenant);
                await tenantRepo.SaveChangesAsync(ct);

                await publisher.PublishAsync(TenantTopics.TenantSuspended,
                    new TenantSuspendedEvent(tenant.Id, tenant.Slug));

                await publisher.PublishAsync(TenantTopics.SubscriptionExpired,
                    new SubscriptionExpiredEvent(sub.TenantId, sub.SubscriptionPlanId));

                _logger.LogWarning("Subscription expired for tenant {TenantId}", sub.TenantId);
                _logger.LogWarning("Tenant {TenantId} was Suspended", sub.TenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process expired subscription for tenant {TenantId}",
                    sub.TenantId);
            }
        }
    }

    private static TimeSpan ToTimeSpan(DelayUnit unit) => unit switch
    {
        DelayUnit.Day => TimeSpan.FromDays(1),
        DelayUnit.Hour => TimeSpan.FromHours(1),
        DelayUnit.Minute => TimeSpan.FromMinutes(1),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
}

public enum DelayUnit
{
    Minute,
    Hour,
    Day
}

// Day => 1
// Hours => 6
// Minute => 5
