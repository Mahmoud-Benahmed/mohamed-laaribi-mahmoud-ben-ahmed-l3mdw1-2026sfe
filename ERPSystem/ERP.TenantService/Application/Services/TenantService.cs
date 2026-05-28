
using ERP.TenantService.Application.DTOs;
using ERP.TenantService.Application.DTOs.Events;
using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;
using ERP.TenantService.Application.Exceptions;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Application.Interfaces.Repositories;
using ERP.TenantService.Application.Interfaces.Services;
using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Messaging;

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

	// For Gateway's Redis cache warmup
	public async Task<List<TenantResponseDto>> GetAllActiveAsync(CancellationToken ct= default)
	{
		var tenants = await _tenantRepository.GetAllActiveAsync(ct);
		return tenants.Select(MapToDto).ToList();
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
			name: dto.Name, 
			email: dto.Email, 
			phone: dto.Phone, 
			subdomainSlug: dto.SubdomainSlug,
			address: dto.Address,
			logoUrl: dto.LogoUrl, 
			primaryColor: dto.PrimaryColor, 
			secondaryColor: dto.SecondaryColor,
			currency: dto.Currency, 
			locale: dto.Locale, 
			timezone: dto.Timezone);


		var subscription = dto.Subscription;
		
		// 3. Auto-assign default plan (Starter) on creation
		var plan = await _planRepository.GetByIdAsync(subscription.SubscriptionPlanId, ct)
			?? throw new InvalidOperationException("Selected plan not found.");

		tenant.AssignSubscription(subscription.SubscriptionPlanId, subscription.StartDate, subscription.Period);
		tenant.Activate();

		await _tenantRepository.AddAsync(tenant);
		await _tenantRepository.SaveChangesAsync();


		// 4. Publish events for downstream services to provision resources
		await _eventPublisher.PublishAsync(TenantTopics.TenantCreated, new TenantCreatedEvent(
			TenantId: tenant.Id, 
			Slug: tenant.Slug, 
			IsActive: tenant.IsActive,
			Name: tenant.Name,
			Address: tenant.Address,
			Email: tenant.Email,
			Phone: tenant.Phone,
			Currency: tenant.Currency
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
			name: dto.Name,
			email: dto.Email,
			phone: dto.Phone,
			subdomainSlug: dto.SubdomainSlug,
			address: dto.Address ?? tenant.Address,
			logoUrl: dto.LogoUrl,
			primaryColor: dto.PrimaryColor,
			secondaryColor: dto.SecondaryColor,
			currency: dto.Currency,
			locale: dto.Locale,
			timezone: dto.Timezone);

		await _tenantRepository.UpdateAsync(tenant);
		await _tenantRepository.SaveChangesAsync(ct);

		await _eventPublisher.PublishAsync(TenantTopics.TenantUpdated, new TenantUpdatedEvent(
			TenantId: tenant.Id,
			OldSlug: tenant.Slug,
			NewSlug: dto.SubdomainSlug,
			IsActive: tenant.IsActive,
			Name: tenant.Name,
			Address: tenant.Address,
			Email: tenant.Email,
			Phone: tenant.Phone,
			Currency: tenant.Currency
		));

		return MapToDto(tenant);
	}

	public async Task DeleteAsync(Guid tenantId, CancellationToken ct = default)
	{
		var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId) 
											?? throw new KeyNotFoundException($"Tenant with id '{tenantId}' not found.");

		if (tenant.Subscription is not null)
		{
			tenant.RemoveSubscription();
			await _subscriptionRepository.DeleteByTenantIdAsync(tenantId, ct);
		}

		tenant.Delete();
		
		// 2. Persist — single save
		await _tenantRepository.UpdateAsync(tenant);
		await _tenantRepository.SaveChangesAsync(ct);
				
		await _eventPublisher.PublishAsync(TenantTopics.TenantSuspended, new TenantSuspendedEvent(
			TenantId: tenant.Id,
			Slug: tenant.Slug
		));
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
		var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(id)
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

		tenant.Suspend();
		await _tenantRepository.UpdateAsync(tenant);
		await _tenantRepository.SaveChangesAsync(ct);

		await _eventPublisher.PublishAsync(TenantTopics.TenantSuspended, new TenantSuspendedEvent(
			TenantId: tenant.Id,
			Slug:tenant.Slug
		));
	}

	public async Task<TenantSubscriptionResponseDto> AssignSubscriptionAsync(Guid tenantId, AssignSubscriptionRequestDto dto, CancellationToken ct = default)
	{
		var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId)
			?? throw new KeyNotFoundException($"Tenant with id '{tenantId}' not found.");
		var subscription = await _subscriptionRepository.GetByTenantIdAsync(tenantId);
		
		if(subscription is not null)
			throw new InvalidOperationException("Unable to assign new plan, current tenant has an active subscription.");

		var newplan = await _planRepository.GetByIdAsync(dto.SubscriptionPlanId)
			?? throw new KeyNotFoundException($"SubscriptionPlan with id '{dto.SubscriptionPlanId}' not found.");

		if (!newplan.IsActive)
			throw new InvalidOperationException("Cannot assign an inactive subscription plan.");
		
		var oldPlanId = tenant.Subscription?.SubscriptionPlanId;

		var wasInactive = !tenant.IsActive;


		tenant.AssignSubscription(dto.SubscriptionPlanId, dto.StartDate, dto.Period);

		if (wasInactive)
			tenant.Activate();

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

		var endDate= dto.Period switch
		{
			SubscriptionPeriodEnum.MONTH => dto.StartDate.AddMonths(1),
			SubscriptionPeriodEnum.YEAR => dto.StartDate.AddYears(1),
			_ => throw new NotImplementedException()
		};

		return new TenantSubscriptionResponseDto(
			tenant.Id, dto.StartDate, endDate, tenant.Subscription!.Period,MapPlanToDto(newplan));
	}

	public async Task RemoveSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
	{
		var tenant = await _tenantRepository.GetByIdWithSubscriptionAsync(tenantId, ct)
			?? throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");

		if (tenant.Subscription is null)
			throw new InvalidOperationException("Tenant has no subscription to remove.");

		var removedPlanId = tenant.Subscription.SubscriptionPlanId;

		tenant.RemoveSubscription();
		tenant.Suspend();

		// 2. Persist — single save
		await _subscriptionRepository.DeleteByTenantIdAsync(tenantId, ct);
		await _tenantRepository.UpdateAsync(tenant);
		await _tenantRepository.SaveChangesAsync(ct);

		// 3. Publish AFTER successful save — order matters:
		//    deactivated first so gateway cache updates before expiry consumers react
		await _eventPublisher.PublishAsync(
			TenantTopics.TenantSuspended,
			new TenantSuspendedEvent(tenant.Id, tenant.Slug));

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

		var plan = subscription.Plan is null
			? null
			: MapPlanToDto(subscription.Plan);

		return new TenantSubscriptionResponseDto(
			subscription.TenantId,
			subscription.StartDate,
			subscription.EndDate,
			subscription.Period,
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
				tenant.Subscription.Period,
                planDto);
		}

        return new TenantResponseDto(
            Id: tenant.Id,
            Name: tenant.Name,
            Email: tenant.Email,
            Phone: tenant.Phone,
            SubdomainSlug: tenant.Slug,
            LogoUrl: tenant.LogoUrl,
            PrimaryColor: tenant.PrimaryColor,
            SecondaryColor: tenant.SecondaryColor,
            Currency: tenant.Currency,
            Locale: tenant.Locale,
            Timezone: tenant.Timezone,
            IsActive: tenant.IsActive,
            IsDeleted: tenant.IsDeleted,
            CreatedAt: tenant.CreatedAt,
            Subscription: subDto
        );
    }

	private static SubscriptionPlanResponseDto MapPlanToDto(SubscriptionPlan plan)
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
