using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Services;

public class SubscriptionPlanService : ISubscriptionPlanService
{
    private readonly ISubscriptionPlanRepository _repository;

    public SubscriptionPlanService(ISubscriptionPlanRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<SubscriptionPlanResponseDto>> GetAllAsync()
    {
        var plans = await _repository.GetAllAsync();
        return plans.Select(MapToDto);
    }

    public async Task<SubscriptionPlanResponseDto?> GetByIdAsync(Guid id)
    {
        var plan = await _repository.GetByIdAsync(id);
        return plan is null ? null : MapToDto(plan);
    }

    public async Task<SubscriptionPlanResponseDto> CreateAsync(CreateSubscriptionPlanRequestDto dto)
    {
        var codeExists = await _repository.CodeExistsAsync(dto.Code);
        if (codeExists)
            throw new InvalidOperationException($"SubscriptionPlan code '{dto.Code}' already exists.");

        var plan = SubscriptionPlan.Create(
            dto.Name,
            dto.Code,
            dto.MonthlyPrice,
            dto.YearlyPrice,
            dto.MaxUsers,
            dto.MaxStorageMb);

        await _repository.AddAsync(plan);
        await _repository.SaveChangesAsync();

        return MapToDto(plan);
    }

    public async Task<SubscriptionPlanResponseDto> UpdateAsync(Guid id, UpdateSubscriptionPlanRequestDto dto)
    {
        var plan = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        var codeExists = await _repository.CodeExistsAsync(dto.Code, id);
        if (codeExists)
            throw new InvalidOperationException($"SubscriptionPlan code '{dto.Code}' already exists.");

        plan.Update(
            dto.Name,
            dto.Code,
            dto.MonthlyPrice,
            dto.YearlyPrice,
            dto.MaxUsers,
            dto.MaxStorageMb);

        await _repository.UpdateAsync(plan);
        await _repository.SaveChangesAsync();

        return MapToDto(plan);
    }

    public async Task ActivateAsync(Guid id)
    {
        var plan = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        plan.Activate();
        await _repository.UpdateAsync(plan);
        await _repository.SaveChangesAsync();
    }

    public async Task DeactivateAsync(Guid id)
    {
        var plan = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"SubscriptionPlan with id '{id}' not found.");

        plan.Deactivate();
        await _repository.UpdateAsync(plan);
        await _repository.SaveChangesAsync();
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
}
