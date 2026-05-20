using ERP.TenantService.Application.DTOs;
using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;
using ERP.TenantService.Application.Events;
using ERP.TenantService.Application.Exceptions;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Messaging;
using ERP.TenantService.Infrastructure.Persistence.Repositories;
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

    public async Task<PagedResultDto<TenantResponseDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);
        var (items, totalCount) = await _tenantRepository.GetAllAsync(page, pageSize, ct);
        return new PagedResultDto<TenantResponseDto>(items.Select(MapToDto).ToList(), totalCount, page, pageSize);
    }

    public async Task<PagedResultDto<TenantResponseDto>> GetDeletedAsync(int page= 1, int pageSize = 10, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);
        var (items, totalCount) = await _tenantRepository.GetDeletedAsync(page, pageSize, ct);
        return new PagedResultDto<TenantResponseDto>(items.Select(MapToDto).ToList(), totalCount, page, pageSize);
    }

    public async Task<TenantResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(id, ct) ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");
        return MapToDto(tenant);
    }

    public async Task<TenantResponseDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetBySlugAsync(slug, ct) ?? throw new KeyNotFoundException($"Tenant with slugn {slug} not found.");
        return MapToDto(tenant);
    }

    public async Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto dto, CancellationToken ct = default)
    {
        // 1. Validate slug uniqueness
        if (await _tenantRepository.SubdomainSlugExistsAsync(dto.SubdomainSlug))
            throw new InvalidOperationException($"Subdomain '{dto.SubdomainSlug}' is already taken.");

        // 2. Create tenant
        var tenant = Tenant.Create(
            dto.Name, dto.Email, dto.Phone, dto.SubdomainSlug,
            dto.LogoUrl, dto.PrimaryColor, dto.SecondaryColor,
            dto.Currency, dto.Locale, dto.Timezone);

        
        // 3. Auto-assign default plan (Starter) on creation
        var starterPlan = await _planRepository.GetByCodeAsync("STARTER", ct)
            ?? throw new InvalidOperationException("Default STARTER plan not configured.");


        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddMonths(1);

        tenant.Activate();
        tenant.AssignSubscription(starterPlan.Id, startDate, endDate);

        await _tenantRepository.AddAsync(tenant);
        await _tenantRepository.SaveChangesAsync();


        // 4. Publish events for downstream services to provision resources
        await _eventPublisher.PublishAsync(TenantTopics.TenantCreated, new TenantCreatedEvent(
            tenant.Id, 
            tenant.Slug, 
            tenant.IsActive
        ));

        return MapToDto(tenant);
    }

    public async Task<TenantResponseDto> UpdateAsync(Guid id, UpdateTenantRequestDto dto, CancellationToken ct = default)
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
        await _tenantRepository.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(TenantTopics.TenantUpdated, new TenantUpdatedEvent(
            tenant.Id,
            tenant.Slug,
            dto.SubdomainSlug,
            tenant.IsActive
        ));

        return MapToDto(tenant);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(id) 
                                            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Delete();

        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(TenantTopics.TenantDeleted, new TenantDeletedEvent(
            tenant.Id,
            tenant.Slug
        ));
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdDeletedAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Restore();

        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(TenantTopics.TenantRestored, new TenantRestoredEvent(
            tenant.Id,
            tenant.Slug,
            tenant.IsActive
        ));
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Activate();
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(TenantTopics.TenantActivated, new TenantActivatedEvent(
            tenant.Id,
            tenant.Slug
        ));
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tenant with id '{id}' not found.");

        tenant.Deactivate();
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(TenantTopics.TenantDeactivated, new TenantDeactivatedEvent(
            TenantId: tenant.Id,
            Slug:tenant.Slug
        ));
    }

    public async Task<TenantSubscriptionResponseDto> AssignSubscriptionAsync(Guid tenantId, AssignSubscriptionRequestDto dto, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId)
            ?? throw new KeyNotFoundException($"Tenant with id '{tenantId}' not found.");

        var newplan = await _planRepository.GetByIdAsync(dto.SubscriptionPlanId)
            ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{dto.SubscriptionPlanId}' not found.");

        if (!newplan.IsActive)
            throw new InvalidOperationException("Cannot assign an inactive subscription plan.");
        
        var oldPlanId = tenant.Subscription?.SubscriptionPlanId;

        if (dto.StartDate >= dto.EndDate)
            throw new ArgumentException("Invalid subscription period: StartDate must be strictly earlier than EndDate.");

        var wasInactive = !tenant.IsActive;
        if (wasInactive)
            tenant.Activate();

        tenant.AssignSubscription(dto.SubscriptionPlanId, dto.StartDate, dto.EndDate);

        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync(ct);
        
        if (wasInactive)
            await _eventPublisher.PublishAsync(
                TenantTopics.TenantActivated,
                new TenantActivatedEvent(tenant.Id, tenant.Slug));

        await _eventPublisher.PublishAsync("tenant.subscription.assigned", new SubscriptionChangedEvent(
            tenantId,
            OldPlanId: oldPlanId ?? Guid.Empty,
            NewPlanId: newplan.Id,
            NewMaxUsers: newplan.MaxUsers,
            NewMaxStorageMb: newplan.MaxStorageMb));

        return new TenantSubscriptionResponseDto(
            tenant.Id, dto.StartDate, dto.EndDate, MapPlanToDto(newplan));
    }

    public async Task RemoveSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");

        if (tenant.Subscription is null)
            throw new InvalidOperationException("Tenant has no subscription to remove.");

        var removedPlanId = tenant.Subscription.SubscriptionPlanId;

        tenant.RemoveSubscription();
        tenant.Deactivate();

        // 2. Persist — single save
        await _subscriptionRepository.DeleteByTenantIdAsync(tenantId, ct);
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync(ct);

        // 3. Publish AFTER successful save — order matters:
        //    deactivated first so gateway cache updates before expiry consumers react
        await _eventPublisher.PublishAsync(
            TenantTopics.TenantDeactivated,
            new TenantDeactivatedEvent(tenant.Id, tenant.Slug));

        await _eventPublisher.PublishAsync(
            TenantTopics.SubscriptionExpired,
            new SubscriptionExpiredEvent(tenantId, removedPlanId));
    }

    public async Task<TenantSubscriptionResponseDto?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId, ct)
            ?? throw new TenantNotFoundException(tenantId);

        if (tenant.Subscription is null)
            throw new TenantSubscriptionNotFoundException(tenantId);

        var subscription = tenant.Subscription;

        var plan = subscription.Plan is not null ? MapPlanToDto(subscription.Plan) : null;

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
            Id:tenant.Id,
            Name: tenant.Name,
            Email: tenant.Email,
            Phone: tenant.Phone,
            SubdomainSlug: tenant.Slug,
            LogoUrl: tenant.LogoUrl,
            PrimaryColor: tenant.PrimaryColor,
            SecondaryColor:tenant.SecondaryColor,
            Currency:tenant.Currency,
            Locale: tenant.Locale,
            Timezone: tenant.Timezone,
            IsActive: tenant.IsActive,
            IsDeleted:tenant.IsDeleted,
            CreatedAt: tenant.CreatedAt,
            subDto);
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

    private static (int Page, int PageSize) NormalizePagination(int page, int pageSize, int defaultPageSize = 10, int maxPageSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > maxPageSize) pageSize = defaultPageSize;
        return (page, pageSize);
    }
}
