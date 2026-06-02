using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Application.Interfaces.Services;
using ERP.TenantService.Properties;
using Microsoft.AspNetCore.Mvc;

namespace ERP.TenantService.Controllers;

[ApiController]
public class SubscriptionPlanController : ControllerBase
{
    private readonly ISubscriptionPlanService _planService;

    public SubscriptionPlanController(ISubscriptionPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet(ApiRoutes.Plans.GetAll)]
    public async Task<IActionResult> GetAllActive(int page, int pageSize, CancellationToken ct = default)
    {
        var plans = await _planService.GetActivePlansAsync(page, pageSize, ct);
        return Ok(plans);
    }

    [HttpGet(ApiRoutes.Plans.GetById)]
    public async Task<IActionResult> GetById([FromRoute]Guid id, CancellationToken ct = default)
    {
        var plan = await _planService.GetByIdAsync(id, ct);
        return Ok(plan);
    }

    [HttpPost(ApiRoutes.Plans.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanRequestDto dto, CancellationToken ct = default)
    {
        var result = await _planService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut(ApiRoutes.Plans.Update)]
    public async Task<IActionResult> Update([FromRoute]Guid id, [FromBody] UpdateSubscriptionPlanRequestDto dto, CancellationToken ct = default)
    {
        var result = await _planService.UpdateAsync(id, dto, ct);
        return Ok(result);
    }


    [HttpDelete(ApiRoutes.Plans.Delete)]
    public async Task<IActionResult> Delete([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _planService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPatch(ApiRoutes.Plans.Activate)]
    public async Task<IActionResult> Activate([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _planService.ActivateAsync(id, ct);
        return NoContent();
    }

    [HttpPatch(ApiRoutes.Plans.Suspend)]
    public async Task<IActionResult> Suspend([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _planService.DeactivateAsync(id, ct);
        return NoContent();
    }
}
