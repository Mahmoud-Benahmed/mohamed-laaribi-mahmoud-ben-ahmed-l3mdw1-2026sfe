using ERP.FournisseurService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP.FournisseurService.Application.Services;


public interface ITenantProvisioningService
{
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly FournisseurDbContext _context;

    public TenantProvisioningService(FournisseurDbContext context)
    {
        _context = context;
    }

    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        await _context.Fournisseurs
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}