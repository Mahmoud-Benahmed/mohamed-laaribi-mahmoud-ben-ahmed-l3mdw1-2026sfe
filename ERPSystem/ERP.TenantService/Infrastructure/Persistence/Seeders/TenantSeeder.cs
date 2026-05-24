using ERP.TenantService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Seeders;

public static class TenantSeeder
{
    public static async Task SeedAsync(TenantDbContext context)
    {
        // ✅ Include all slugs you check below
        var slugs = new[] { "acme", "xyz", "bard", "nord" };

        var existingSlugs = await context.Tenants
            .IgnoreQueryFilters()
            .Where(t => slugs.Contains(t.Slug))
            .Select(t => t.Slug)
            .ToListAsync();

        var plans = await context.SubscriptionPlans.ToListAsync();

        if (!plans.Any())
            throw new InvalidOperationException("No subscription plans found. Run SubscriptionPlanSeeder first.");

        var tenants = new List<Tenant>();

        if (!existingSlugs.Contains("acme"))
            tenants.Add(Tenant.Create(
                name: "Acme Corporation",
                email: "contact@acme.erp.com",
                phone: "+21671123456",
                subdomainSlug: "acme",
                logoUrl: null,
                primaryColor: "#2563EB",
                secondaryColor: "#1E40AF",
                currency: "TND",
                locale: "fr-TN",
                timezone: "Africa/Tunis"
            ));

        if (!existingSlugs.Contains("xyz"))
            tenants.Add(Tenant.Create(
                name: "XYZ Global Solutions",
                email: "admin@xyzglobal.com",
                phone: "+21652234567",
                subdomainSlug: "xyz",
                logoUrl: null,
                primaryColor: "#7C3AED",
                secondaryColor: "#5B21B6",
                currency: "EUR",
                locale: "fr-FR",
                timezone: "Europe/Paris"
            ));

        if (!existingSlugs.Contains("bard"))
            tenants.Add(Tenant.Create(
                name: "Bard Innovations",
                email: "hello@bard.ai",
                phone: "+21698888777",
                subdomainSlug: "bard",
                logoUrl: null,
                primaryColor: "#059669",
                secondaryColor: "#047857",
                currency: "USD",
                locale: "en-US",
                timezone: "America/New_York"
            ));

        if (tenants.Any())
        {
            var rand = new Random();
            foreach (var tenant in tenants)
            {
                if (tenant.IsDeleted) continue;

                var plan = plans[rand.Next(plans.Count)];

                var periods = Enum.GetValues<SubscriptionPeriodEnum>();
                var randomPeriod = periods[rand.Next(periods.Length)];

                var startDate = DateTime.UtcNow;

                var endDate = randomPeriod switch
                {
                    SubscriptionPeriodEnum.MONTH => startDate.AddMonths(1),
                    SubscriptionPeriodEnum.YEAR => startDate.AddYears(1),
                    _ => throw new ArgumentOutOfRangeException()
                };

                tenant.Activate();

                tenant.AssignSubscription(
                    plan.Id,
                    startDate,
                    randomPeriod
                );
            }

            await context.Tenants.AddRangeAsync(tenants);
            await context.SaveChangesAsync();
        }
    }
}
