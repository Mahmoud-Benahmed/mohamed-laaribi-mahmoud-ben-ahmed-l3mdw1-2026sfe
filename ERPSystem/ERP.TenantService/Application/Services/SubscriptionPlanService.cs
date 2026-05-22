using ERP.TenantService.Application.DTOs;
using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Services;

public class SubscriptionPlanService : ISubscriptionPlanService
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepo;

    public SubscriptionPlanService(ISubscriptionPlanRepository repository, ITenantSubscriptionRepository tenantSubscriptionRepo)
    {
        _repository = repository;
        _tenantSubscriptionRepo = tenantSubscriptionRepo;
    }

    public async Task<PagedResultDto<SubscriptionPlanResponseDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);
        var (items, totalCount) = await _repository.GetAllAsync(page, pageSize, ct);
        return new PagedResultDto<SubscriptionPlanResponseDto>(
            items.Select(MapToDto).ToList(), 
            totalCount, 
            page, pageSize);
    }

    public async Task<SubscriptionPlanResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");
        return MapToDto(plan);
    }

    public async Task<SubscriptionPlanResponseDto> CreateAsync(CreateSubscriptionPlanRequestDto dto, CancellationToken ct = default)
    {
        var codeExists = await _repository.CodeExistsAsync(dto.Code, null, ct);
        if (codeExists)
            throw new InvalidOperationException($"SubscriptionPlan code '{dto.Code}' already exists.");

        var plan = SubscriptionPlan.Create(dto.Name, dto.Code,dto.MonthlyPrice, dto.YearlyPrice, dto.MaxUsers,dto.MaxStorageMb);

        await _repository.AddAsync(plan);
        await _repository.SaveChangesAsync(ct);

        return MapToDto(plan);
    }

    public async Task<SubscriptionPlanResponseDto> UpdateAsync(Guid id, UpdateSubscriptionPlanRequestDto dto, CancellationToken ct = default)
    {
        var plan = await _repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        var codeExists = await _repository.CodeExistsAsync(dto.Code, id, ct);
        if (codeExists)
            throw new InvalidOperationException($"SubscriptionPlan code '{dto.Code}' already exists.");

        plan.Update( dto.Name, dto.Code,dto.MonthlyPrice, dto.YearlyPrice, dto.MaxUsers, dto.MaxStorageMb);

        await _repository.UpdateAsync(plan);
        await _repository.SaveChangesAsync(ct);

        return MapToDto(plan);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        var tenantSubscriptions = await _tenantSubscriptionRepo.GetActiveBySubscriptionPlanIdAsync(id, DateTime.UtcNow, ct);
        if (tenantSubscriptions.Any())
            throw new InvalidOperationException($"Cannot delete SubscriptionPlan: It is assigned to ({tenantSubscriptions.Count}) tenant(s).");

        await _repository.DeleteAsync(plan);
        await _repository.SaveChangesAsync(ct);
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        plan.Activate();
        await _repository.UpdateAsync(plan);
        await _repository.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        plan.Suspend();
        await _repository.UpdateAsync(plan);
        await _repository.SaveChangesAsync(ct);
    }

    private static SubscriptionPlanResponseDto MapToDto(SubscriptionPlan plan)
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

    private static (int Page, int PageSize) NormalizePagination(int page, int pageSize, int defaultPageSize= 10,int maxPageSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = defaultPageSize;
        if (pageSize > maxPageSize) pageSize = maxPageSize;
        return (page, pageSize);
    }
}
