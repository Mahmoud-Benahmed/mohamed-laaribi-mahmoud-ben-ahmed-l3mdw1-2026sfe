namespace ERP.TenantService.Domain;

public class TenantSubscription
{
    public Guid TenantId { get; private set; }
    public Guid SubscriptionPlanId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }

    public SubscriptionPlan? Plan { get; private set; }

    private TenantSubscription() { }

    public static TenantSubscription Create(
        Guid tenantId,
        Guid subscriptionPlanId,
        DateTime startDate,
        DateTime endDate)
    {
        return new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = subscriptionPlanId,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    public void Update(Guid subscriptionPlanId, DateTime startDate, DateTime endDate)
    {
        SubscriptionPlanId = subscriptionPlanId;
        StartDate = startDate;
        EndDate = endDate;
    }
}
