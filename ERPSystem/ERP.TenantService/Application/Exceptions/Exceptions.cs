namespace ERP.TenantService.Application.Exceptions;

public class TenantNotFoundException(Guid id)
    : Exception($"Tenant with id '{id}' was not found.");

public class TenantSubscriptionNotFoundException(Guid tenantId)
    : Exception($"No subscription found for tenant '{tenantId}'.");

public class SubscriptionPlanNotFoundException(Guid id)
    : Exception($"SubscriptionPlan with id '{id}' was not found.");

public class SubdomainAlreadyTakenException(string slug)
    : Exception($"Subdomain '{slug}' is already taken.");