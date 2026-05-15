namespace ERP.TenantService.Properties;

public static class ApiRoutes
{
    public static class Tenants
    {
        private const string Base = "tenants";

        public const string GetAll = Base;
        public const string GetById = Base + "/{id:guid}";
        public const string GetBySubdomain = Base + "/subdomain/{slug}";
        public const string Create = Base;
        public const string Update = Base + "/{id:guid}";
        public const string Delete = Base + "/{id:guid}";
        public const string Activate = Base + "/{id:guid}/activate";
        public const string Deactivate = Base + "/{id:guid}/deactivate";
        public const string AssignSubscription = Base + "/{id:guid}/subscription";
        public const string GetSubscription = Base + "/{id:guid}/subscription";
    }

    public static class Plans
    {
        private const string Base = "plans";

        public const string GetAll = Base;
        public const string GetById = Base + "/{id:guid}";
        public const string Create = Base;
        public const string Update = Base + "/{id:guid}";
        public const string Activate = Base + "/{id:guid}/activate";
        public const string Deactivate = Base + "/{id:guid}/deactivate";
    }
}
