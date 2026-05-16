using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Properties;
using Microsoft.AspNetCore.Mvc;

namespace ERP.TenantService.Controllers;

[ApiController]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet(ApiRoutes.Tenants.GetAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page,
        [FromQuery] int pageSize, CancellationToken ct = default)
    {
       
        var result = await _tenantService.GetAllAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet(ApiRoutes.Tenants.GetDeleted)]
    public async Task<IActionResult> GetDeleted(
    [FromQuery] int page,
    [FromQuery] int pageSize, CancellationToken ct = default)
    {
        var result = await _tenantService.GetDeletedAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet(ApiRoutes.Tenants.GetById)]
    public async Task<IActionResult> GetById([FromRoute]Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantService.GetByIdAsync(id, ct);
        return Ok(tenant);
    }

    [HttpGet(ApiRoutes.Tenants.GetBySlug)]
    public async Task<IActionResult> GetBySlug([FromRoute] string slug, CancellationToken ct = default)
    {
        var tenant = await _tenantService.GetBySlugAsync(slug, ct);
        return Ok(tenant);
    }

    [HttpPost(ApiRoutes.Tenants.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequestDto dto, CancellationToken ct = default)
    {
        var result = await _tenantService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut(ApiRoutes.Tenants.Update)]
    public async Task<IActionResult> Update([FromRoute]Guid id, [FromBody] UpdateTenantRequestDto dto, CancellationToken ct = default)
    {
        var result = await _tenantService.UpdateAsync(id, dto, ct);
        return Ok(result);
    }

    [HttpDelete(ApiRoutes.Tenants.Delete)]
    public async Task<IActionResult> Delete([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _tenantService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPatch(ApiRoutes.Tenants.Restore)]
    public async Task<IActionResult> Restore([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _tenantService.RestoreAsync(id, ct);
        return NoContent();
    }

    [HttpPatch(ApiRoutes.Tenants.Activate)]
    public async Task<IActionResult> Activate([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _tenantService.ActivateAsync(id, ct);
        return NoContent();
    }

    [HttpPatch(ApiRoutes.Tenants.Deactivate)]
    public async Task<IActionResult> Deactivate([FromRoute]Guid id, CancellationToken ct = default)
    {
        await _tenantService.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost(ApiRoutes.Tenants.AssignSubscription)]
    public async Task<IActionResult> AssignSubscription([FromRoute]Guid id, [FromBody] AssignSubscriptionRequestDto dto, CancellationToken ct = default)
    {
        var result = await _tenantService.AssignSubscriptionAsync(id, dto, ct);
        return Ok(result);
    }

    [HttpGet(ApiRoutes.Tenants.GetSubscription)]
    public async Task<IActionResult> GetSubscription([FromRoute]Guid id, CancellationToken ct = default)
    {
        var result = await _tenantService.GetSubscriptionAsync(id, ct);
        return Ok(result);
    }
}
