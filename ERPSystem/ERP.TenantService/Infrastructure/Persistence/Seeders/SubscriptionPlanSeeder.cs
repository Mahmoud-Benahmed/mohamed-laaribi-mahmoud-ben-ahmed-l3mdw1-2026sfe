using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Seeders;

public static class SubscriptionPlanSeeder
{
    public static async Task SeedAsync(TenantDbContext context)
    {
        var codes = new[] { "STARTER", "PRO", "ENTERPRISE" };

        var existingPlans = await context.SubscriptionPlans
            .Where(p => codes.Contains(p.Code))
            .ToListAsync();
        //Starter 
        var starter = existingPlans.FirstOrDefault(p => p.Code == "STARTER");
        if (starter is null)
        {
            context.SubscriptionPlans.Add(SubscriptionPlan.Create(
                name: "Starter",
                code: "STARTER",
                monthlyPrice: 29.00m,
                yearlyPrice: 24.00m * 12, 
                maxUsers: 5,
                maxStorageMb: 1024));         
        }
        else
        {
            starter.Update(
                name: "Starter",
                code: "STARTER",
                monthlyPrice: 29.00m,
                yearlyPrice: 24.00m * 12,
                maxUsers: 5,
                maxStorageMb: 1024);
        }

        // Pro
        var pro = existingPlans.FirstOrDefault(p => p.Code == "PRO");
        if (pro is null)
        {
            context.SubscriptionPlans.Add(SubscriptionPlan.Create(
                name: "Pro",
                code: "PRO",
                monthlyPrice: 79.00m,
                yearlyPrice: 66.00m * 12,  
                maxUsers: 25,
                maxStorageMb: 10240));        
        }
        else
        {
            pro.Update(
                name: "Pro",
                code: "PRO",
                monthlyPrice: 79.00m,
                yearlyPrice: 66.00m * 12,
                maxUsers: 25,
                maxStorageMb: 10240);
        }

        //Enterprise 
        var enterprise = existingPlans.FirstOrDefault(p => p.Code == "ENTERPRISE");
        if (enterprise is null)
        {
            context.SubscriptionPlans.Add(SubscriptionPlan.Create(
                name: "Enterprise",
                code: "ENTERPRISE",
                monthlyPrice: 199.00m,
                yearlyPrice: 166.00m * 12,  
                maxUsers: 200,
                maxStorageMb: 102400));      
        }
        else
        {
            enterprise.Update(
                name: "Enterprise",
                code: "ENTERPRISE",
                monthlyPrice: 199.00m,
                yearlyPrice: 166.00m * 12,
                maxUsers: 200,
                maxStorageMb: 102400);
        }

        await context.SaveChangesAsync();
    }
}