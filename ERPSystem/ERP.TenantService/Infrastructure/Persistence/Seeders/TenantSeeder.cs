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

        if (!existingSlugs.Contains("nord"))
        {
            Tenant deletedTenant = Tenant.Create(
                                        name: "Nordic Tech OÜ",
                                        email: "info@nordtech.ee",
                                        phone: "+37255501234",
                                        subdomainSlug: "nord",
                                        logoUrl: null,
                                        primaryColor: "#0EA5E9",
                                        secondaryColor: "#0369A1",
                                        currency: "EUR",
                                        locale: "et-EE",
                                        timezone: "Europe/Tallinn"
                                    );
            deletedTenant.Delete();
            tenants.Add(deletedTenant);
        }

        if (tenants.Any())
        {
            var rand = new Random();
            foreach (var tenant in tenants)
            {
                if (tenant.IsDeleted) continue;

                // ✅ Use plans.Count, not tenants.Count
                var plan = plans[rand.Next(0, plans.Count)];
                var startDate = DateTime.UtcNow.AddDays(rand.Next(30));
                var endDate = startDate.AddMonths(rand.Next(1, 13)); // ✅ endDate based on startDate

                tenant.Activate();
                tenant.AssignSubscription(plan.Id, startDate, endDate);
            }

            await context.Tenants.AddRangeAsync(tenants);
            await context.SaveChangesAsync();
        }
    }
}
