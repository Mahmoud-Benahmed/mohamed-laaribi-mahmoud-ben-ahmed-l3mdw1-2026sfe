namespace ERP.TenantService.Application.Exceptions;

public class TenantNotFoundException : Exception
{
    public TenantNotFoundException(Guid id) : base($"Tenant with id '{id}' was not found.") { }
    public TenantNotFoundException(string slug) : base($"Tenant with slug '{slug}' was not found.") { }
    public TenantNotFoundException() : base("Tenant was not found.") { }
}

public class TenantSubscriptionNotFoundException(Guid tenantId) : Exception($"No subscription found for tenant '{tenantId}'.");
public class SubscriptionPlanNotFoundException(Guid id) : Exception($"SubscriptionPlan with id '{id}' was not found.");
public class SubdomainAlreadyTakenException() : Exception($"Subdomain is invlaid.");
public class EmailAlreadyTakenException() : Exception($"Invalid email address.");
public class UnableDeleteTenantHasActiveSubscriptionException() : Exception("Cannot delete/suspend current tenant, it has an active subscription.");
public class TenantAlreadyExistsException(string field, string value) : Exception($"A tenant with {field} '{value}' already exists.");
public class TenantAlreadyActiveException(Guid id) : Exception($"Tenant '{id}' is already active.");
public class TenantAlreadyDeletedException(Guid id) : Exception($"Tenant '{id}' is already deleted.");
public class TenantHasActiveSubscriptionException(Guid id) : Exception($"Tenant '{id}' has an active subscription and cannot be deleted.");
public class SubscriptionPlanAlreadyExistsException(string code) : Exception($"Subscription plan with code '{code}' already exists.");
public class SubscriptionPlanInactiveException(Guid id) : Exception($"Subscription plan '{id}' is inactive and cannot be assigned.");
public class SubscriptionPlanInUseException(Guid id, int tenantCount) : Exception($"Subscription plan '{id}' is assigned to {tenantCount} tenant(s) and cannot be deleted.");
public class SubscriptionAlreadyExistsException(Guid tenantId) : Exception($"Tenant '{tenantId}' already has an active subscription.");
public class SubscriptionAssignmentFailedException(string reason) : Exception($"Failed to assign subscription: {reason}");
public class DuplicateKeyException(string key) : Exception(key);