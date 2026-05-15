using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Repositories;

public class TenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly TenantDbContext _context;

    public TenantSubscriptionRepository(TenantDbContext context)
    {
        _context = context;
    }

    public async Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId)
    {
        return await _context.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
