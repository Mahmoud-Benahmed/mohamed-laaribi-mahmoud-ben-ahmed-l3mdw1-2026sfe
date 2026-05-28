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
            decimal starterMonthlyPrice = 29.00m;
            plans.Add(SubscriptionPlan.Create(
                name: "Starter",
                code: "STARTER",
                monthlyPrice: 29.00m,
                yearlyPrice: ApplyDiscount(20, starterMonthlyPrice*12),
                maxUsers: 5,
                maxStorageMb: 1024));
        }

        if (!existingCodes.Contains("PRO"))
        {
            decimal proMonthlyPrice = 79.00m;
            plans.Add(SubscriptionPlan.Create(
                name: "Pro",
                code: "PRO",
                monthlyPrice: 79.00m,
                yearlyPrice: ApplyDiscount(20, proMonthlyPrice * 12),
                maxUsers: 25,
                maxStorageMb: 10240));
        }

        if (!existingCodes.Contains("ENTERPRISE"))
        {
            decimal entrMonthlyPrice = 199.00m;
            plans.Add(SubscriptionPlan.Create(
                name: "Enterprise",
                code: "ENTERPRISE",
                monthlyPrice: entrMonthlyPrice,
                yearlyPrice: ApplyDiscount(20, entrMonthlyPrice * 12),
                maxUsers: 200,
                maxStorageMb: 102400));
        }

        if (plans.Any())
        {
            await context.SubscriptionPlans.AddRangeAsync(plans);
            await context.SaveChangesAsync();
        }
    }

    private static decimal ApplyDiscount(int percentageToDiscount, decimal from)
    {
        if (percentageToDiscount >= 100) return 0m;
        if (percentageToDiscount <= 0) return from;

        decimal discounted = from * (100 - percentageToDiscount) / 100m;
        // Use conventional rounding for money
        return Math.Round(discounted, 2, MidpointRounding.AwayFromZero);
    }
}
