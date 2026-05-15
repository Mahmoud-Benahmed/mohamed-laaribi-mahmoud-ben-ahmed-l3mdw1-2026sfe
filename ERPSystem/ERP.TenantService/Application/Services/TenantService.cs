using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Messaging;
using ERP.TenantService.Application.Events;
using ERP.TenantService.Infrastructure.Messaging;
namespace ERP.TenantService.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly IEventPublisher _eventPublisher;

    public TenantService(
        ITenantRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        IEventPublisher eventPublisher)
    {
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<(IEnumerable<TenantResponseDto> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
    {
        var tenants = await _tenantRepository.GetAllAsync(page, pageSize);
        var total = await _tenantRepository.CountAsync();
        return (tenants.Select(MapToDto), total);
    }

    public async Task<TenantResponseDto?> GetByIdAsync(Guid id)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(id);
        return tenant is null ? null : MapToDto(tenant);
    }

    public async Task<TenantResponseDto?> GetBySubdomainSlugAsync(string slug)
    {
        var tenant = await _tenantRepository.GetBySubdomainSlugAsync(slug);
        return tenant is null ? null : MapToDto(tenant);
    }

    public async Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto dto)
    {
        var slugExists = await _tenantRepository.SubdomainSlugExistsAsync(dto.SubdomainSlug);
        if (slugExists)
            throw new InvalidOperationException($"Subdomain slug '{dto.SubdomainSlug}' is already taken.");

        var tenant = Tenant.Create(
            dto.Name,
            dto.Email,
            dto.Phone,
            dto.SubdomainSlug,
            dto.LogoUrl,
            dto.PrimaryColor,
            dto.SecondaryColor,
            dto.Currency,
            dto.Locale,
            dto.Timezone);

        await _tenantRepository.AddAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        await _eventPublisher.PublishAsync("tenant.created", new { tenant.Id, tenant.Name, tenant.SubdomainSlug });

        return MapToDto(tenant);
    }

    public async Task<TenantResponseDto> UpdateAsync(Guid id, UpdateTenantRequestDto dto)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        var slugExists = await _tenantRepository.SubdomainSlugExistsAsync(dto.SubdomainSlug, id);
        if (slugExists)
            throw new InvalidOperationException($"Subdomain slug '{dto.SubdomainSlug}' is already taken.");

        tenant.Update(
            dto.Name,
            dto.Email,
            dto.Phone,
            dto.SubdomainSlug,
            dto.LogoUrl,
            dto.PrimaryColor,
            dto.SecondaryColor,
            dto.Currency,
            dto.Locale,
            dto.Timezone);

        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        return MapToDto(tenant);
    }

    public async Task DeleteAsync(Guid id)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Deactivate();
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        await _eventPublisher.PublishAsync("tenant.deactivated", new { tenant.Id });
    }

    public async Task ActivateAsync(Guid id)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Activate();
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        await _eventPublisher.PublishAsync(TenantTopics.TenantActivated, new TenantActivatedEvent(
            TenantId: tenant.Id,
            TenantName: tenant.Name,
            SubdomainSlug: tenant.SubdomainSlug
        ));
    }

    public async Task DeactivateAsync(Guid id)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Deactivate();
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync();
    }

    public async Task<TenantSubscriptionResponseDto> AssignSubscriptionAsync(Guid tenantId, AssignSubscriptionRequestDto dto)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId)
            ?? throw new KeyNotFoundException($"Tenant with id '{tenantId}' not found.");

        var plan = await _planRepository.GetByIdAsync(dto.SubscriptionPlanId)
            ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{dto.SubscriptionPlanId}' not found.");

        if (!plan.IsActive)
            throw new InvalidOperationException("Cannot assign an inactive subscription plan.");

        tenant.AssignSubscription(dto.SubscriptionPlanId, dto.StartDate, dto.EndDate);

        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        await _eventPublisher.PublishAsync("tenant.subscription.assigned", new
        {
            TenantId = tenantId,
            PlanId = dto.SubscriptionPlanId
        });

        return MapSubscriptionToDto(tenant.Subscription!, plan);
    }

    public async Task<TenantSubscriptionResponseDto?> GetSubscriptionAsync(Guid tenantId)
    {
        var subscription = await _subscriptionRepository.GetByTenantIdAsync(tenantId);
        if (subscription is null) return null;

        var plan = subscription.Plan is not null
            ? MapPlanToDto(subscription.Plan)
            : null;

        return new TenantSubscriptionResponseDto(
            subscription.TenantId,
            subscription.StartDate,
            subscription.EndDate,
            plan);
    }

    private static TenantResponseDto MapToDto(Tenant tenant)
    {
        TenantSubscriptionResponseDto? subDto = null;
        if (tenant.Subscription is not null)
        {
            var planDto = tenant.Subscription.Plan is not null ? MapPlanToDto(tenant.Subscription.Plan) : null;
            subDto = new TenantSubscriptionResponseDto(
                tenant.Subscription.TenantId,
                tenant.Subscription.StartDate,
                tenant.Subscription.EndDate,
                planDto);
        }

        return new TenantResponseDto(
            tenant.Id,
            tenant.Name,
            tenant.Email,
            tenant.Phone,
            tenant.SubdomainSlug,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.Currency,
            tenant.Locale,
            tenant.Timezone,
            tenant.IsActive,
            tenant.CreatedAt,
            subDto);
    }

    private static TenantSubscriptionResponseDto MapSubscriptionToDto(
        Domain.TenantSubscription sub, Domain.SubscriptionPlan plan)
    {
        return new TenantSubscriptionResponseDto(
            sub.TenantId,
            sub.StartDate,
            sub.EndDate,
            MapPlanToDto(plan));
    }

    private static SubscriptionPlanResponseDto MapPlanToDto(Domain.SubscriptionPlan plan)
    {
        return new SubscriptionPlanResponseDto(
            plan.Id,
            plan.Name,
            plan.Code,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.MaxUsers,
            plan.MaxStorageMb,
            plan.IsActive);
    }
}
