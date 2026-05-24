namespace ERP.TenantService.Domain;

public class TenantSubscription
{
    public Guid TenantId { get; private set; }
    public Guid SubscriptionPlanId { get; private set; }
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public SubscriptionPeriodEnum Period { get; init; }
    public SubscriptionPlan? Plan { get; private set; }

    private TenantSubscription() { }

    public static TenantSubscription Create(
        Guid tenantId,
        Guid subscriptionPlanId,
        DateTimeOffset startDate,
        SubscriptionPeriodEnum period)
    {
        DateTimeOffset endDate = ComputeEndDate(startDate, period);

        return new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = subscriptionPlanId,
            StartDate = startDate,
            EndDate = endDate,
            Period = period
        };
    }

    public void Update(Guid subscriptionPlanId, DateTimeOffset startDate, DateTimeOffset endDate) // ← was DateTime
    {
        SubscriptionPlanId = subscriptionPlanId;
        StartDate = startDate;
        EndDate = endDate;
    }

    private static DateTimeOffset ComputeEndDate(
        DateTimeOffset startDate,
        SubscriptionPeriodEnum period)
    {
        return period switch
        {
            SubscriptionPeriodEnum.MONTH => startDate.AddMonths(1),
            SubscriptionPeriodEnum.YEAR => startDate.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }
}

public enum SubscriptionPeriodEnum
{
    YEAR,
    MONTH
}
