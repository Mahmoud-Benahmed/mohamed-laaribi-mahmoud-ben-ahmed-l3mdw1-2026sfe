using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Seeders;

public static class SubscriptionPlanSeeder
{
    public static async Task SeedAsync(TenantDbContext context)
    {
        var codes = new[] { "STARTER", "PRO", "ENTERPRISE" };

        var existingCodes = await context.SubscriptionPlans
            .Where(p => codes.Contains(p.Code))
            .Select(p => p.Code)
            .ToListAsync();

        var plans = new List<SubscriptionPlan>();

        if (!existingCodes.Contains("STARTER"))
        {
            plans.Add(SubscriptionPlan.Create(
                name: "Starter",
                code: "STARTER",
                monthlyPrice: 29.00m,
                yearlyPrice: 290.00m,
                maxUsers: 5,
                maxStorageMb: 1024));
        }

        if (!existingCodes.Contains("PRO"))
        {
            plans.Add(SubscriptionPlan.Create(
                name: "Pro",
                code: "PRO",
                monthlyPrice: 79.00m,
                yearlyPrice: 790.00m,
                maxUsers: 25,
                maxStorageMb: 10240));
        }

        if (!existingCodes.Contains("ENTERPRISE"))
        {
            plans.Add(SubscriptionPlan.Create(
                name: "Enterprise",
                code: "ENTERPRISE",
                monthlyPrice: 199.00m,
                yearlyPrice: 1990.00m,
                maxUsers: 200,
                maxStorageMb: 102400));
        }

        if (plans.Any())
        {
            await context.SubscriptionPlans.AddRangeAsync(plans);
            await context.SaveChangesAsync();
        }
    }
}
