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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, total) = await _tenantService.GetAllAsync(page, pageSize);

        return Ok(new
        {
            data = items,
            page,
            pageSize,
            totalCount = total
        });
    }

    [HttpGet(ApiRoutes.Tenants.GetById)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenant = await _tenantService.GetByIdAsync(id);
        return tenant is null ? NotFound(new { statusCode = 404, code = "NOT_FOUND", message = $"Tenant '{id}' not found." }) : Ok(tenant);
    }

    [HttpGet(ApiRoutes.Tenants.GetBySubdomain)]
    public async Task<IActionResult> GetBySubdomain(string slug)
    {
        var tenant = await _tenantService.GetBySubdomainSlugAsync(slug);
        return tenant is null ? NotFound(new { statusCode = 404, code = "NOT_FOUND", message = $"Tenant with subdomain '{slug}' not found." }) : Ok(tenant);
    }

    [HttpPost(ApiRoutes.Tenants.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                statusCode = 400,
                code = "VALIDATION_ERROR",
                message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
            });

        var result = await _tenantService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut(ApiRoutes.Tenants.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                statusCode = 400,
                code = "VALIDATION_ERROR",
                message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
            });

        var result = await _tenantService.UpdateAsync(id, dto);
        return Ok(result);
    }

    [HttpDelete(ApiRoutes.Tenants.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _tenantService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPut(ApiRoutes.Tenants.Activate)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _tenantService.ActivateAsync(id);
        return NoContent();
    }

    [HttpPut(ApiRoutes.Tenants.Deactivate)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _tenantService.DeactivateAsync(id);
        return NoContent();
    }

    [HttpPost(ApiRoutes.Tenants.AssignSubscription)]
    public async Task<IActionResult> AssignSubscription(Guid id, [FromBody] AssignSubscriptionRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                statusCode = 400,
                code = "VALIDATION_ERROR",
                message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
            });

        var result = await _tenantService.AssignSubscriptionAsync(id, dto);
        return Ok(result);
    }

    [HttpGet(ApiRoutes.Tenants.GetSubscription)]
    public async Task<IActionResult> GetSubscription(Guid id)
    {
        var result = await _tenantService.GetSubscriptionAsync(id);
        return result is null ? NotFound(new { statusCode = 404, code = "NOT_FOUND", message = $"No subscription found for tenant '{id}'." }) : Ok(result);
    }
}
