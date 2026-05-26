using ERP.ClientService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP.ClientService.Application.Services;


public interface ITenantProvisioningService
{
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly ClientDbContext _context;

    public TenantProvisioningService(ClientDbContext context)
    {
        _context = context;
    }

    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        await _context.Clients
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}