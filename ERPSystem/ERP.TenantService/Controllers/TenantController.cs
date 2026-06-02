using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Application.Interfaces.Services;
using ERP.TenantService.Properties;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.Pkcs;

namespace ERP.TenantService.Controllers;

[ApiController]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }
    [HttpGet(ApiRoutes.Tenants.GetAllActive)]
    public async Task<IActionResult> GetAllActive(CancellationToken ct = default)
    {
        return Ok(await _tenantService.GetAllActiveAsync(ct));
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

    [HttpGet(ApiRoutes.Tenants.GetTenantSettings)]
    public async Task<IActionResult> GetTenantSettings([FromRoute] Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantService.GetTenantSettings(id, ct);
        return Ok(tenant);
    }

    [HttpGet(ApiRoutes.Tenants.GetBySlug)]
    public async Task<IActionResult> GetBySlug([FromRoute] string slug, CancellationToken ct = default)
    {
        var tenant = await _tenantService.GetBySlugAsync(slug, ct);
        return Ok(tenant);
    }

    [HttpGet(ApiRoutes.Tenants.GetBrandingBySlug)]
    public async Task<ActionResult<TenantBrandingDto>> GetBranding([FromRoute] string slug)
    {
        var tenant = await _tenantService.GetBySlugAsync(slug);
        if (tenant is null) return NotFound();

        return Ok(new TenantBrandingDto(
            Name: tenant.Name,
            LogoUrl: tenant.LogoUrl,
            PrimaryColor: tenant.PrimaryColor,
            SecondaryColor: tenant.SecondaryColor,
            Currency: tenant.Currency,
            Locale: tenant.Locale,
            Timezone: tenant.Timezone,
            IsActive: tenant.IsActive
        ));
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

    [HttpPatch(ApiRoutes.Tenants.Suspend)]
    public async Task<IActionResult> Suspend([FromRoute]Guid id, CancellationToken ct = default)
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

    // DELETE /api/tenants/{id}/subscription
    [HttpDelete(ApiRoutes.Tenants.RemoveSubscription)]
    public async Task<IActionResult> RemoveSubscription([FromRoute] Guid id, CancellationToken ct)
    {
        await _tenantService.RemoveSubscriptionAsync(id, ct);
        return NoContent();
    }

    [HttpGet(ApiRoutes.Tenants.GetSubscription)]
    public async Task<IActionResult> GetSubscription([FromRoute]Guid id, CancellationToken ct = default)
    {
        var result = await _tenantService.GetSubscriptionAsync(id, ct);
        return Ok(result);
    }
}
