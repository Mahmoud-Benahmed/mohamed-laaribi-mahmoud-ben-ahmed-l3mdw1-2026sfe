using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Application.Interfaces;
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
    public async Task<IActionResult> GetAll()
    {
        var plans = await _planService.GetAllAsync();
        return Ok(plans);
    }

    [HttpGet(ApiRoutes.Plans.GetById)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var plan = await _planService.GetByIdAsync(id);
        return plan is null
            ? NotFound(new { statusCode = 404, code = "NOT_FOUND", message = $"SubscriptionPlan '{id}' not found." })
            : Ok(plan);
    }

    [HttpPost(ApiRoutes.Plans.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                statusCode = 400,
                code = "VALIDATION_ERROR",
                message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
            });

        var result = await _planService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut(ApiRoutes.Plans.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                statusCode = 400,
                code = "VALIDATION_ERROR",
                message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
            });

        var result = await _planService.UpdateAsync(id, dto);
        return Ok(result);
    }

    [HttpPatch(ApiRoutes.Plans.Activate)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _planService.ActivateAsync(id);
        return NoContent();
    }

    [HttpPatch(ApiRoutes.Plans.Deactivate)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _planService.DeactivateAsync(id);
        return NoContent();
    }
}
