namespace ERP.AuthService.Properties;

public static class Privileges
{
    public static class Users
    {
        public const string MANAGE_USERS = "MANAGE_USERS";
        public const string VIEW_USERS = "VIEW_USERS";
        public const string CREATE_USER = "CREATE_USER";
        public const string UPDATE_USER = "UPDATE_USER";
        public const string DELETE_USER = "DELETE_USER";
        public const string RESTORE_USER = "RESTORE_USER";
        public const string ACTIVATE_USER = "ACTIVATE_USER";
        public const string DEACTIVATE_USER = "DEACTIVATE_USER";
        public const string ASSIGN_ROLES = "ASSIGN_ROLES";
        public const string CREATE_ROLE = "CREATE_ROLE";
        public const string UPDATE_ROLE = "UPDATE_ROLE";
        public const string DELETE_ROLE = "DELETE_ROLE";
        public const string CREATE_CONTROLE = "CREATE_CONTROLE";
        public const string UPDATE_CONTROLE = "UPDATE_CONTROLE";
        public const string DELETE_CONTROLE = "DELETE_CONTROLE";
        public const string BUY_SUBSCRIPTION = "BUY_SUBSCRIPTION";
        public const string EDIT_SYSTEM_SETTINGS = "EDIT_SYSTEM_SETTINGS";
    }

    public static class Audit
    {
        public const string MANAGE_AUDITLOGS = "MANAGE_AUDITLOGS";
    }

    public static class Clients
    {
        public const string MANAGE_CLIENTS = "MANAGE_CLIENTS";
        public const string VIEW_CLIENTS = "VIEW_CLIENTS";
        public const string CREATE_CLIENT = "CREATE_CLIENT";
        public const string UPDATE_CLIENT = "UPDATE_CLIENT";
        public const string DELETE_CLIENT = "DELETE_CLIENT";
        public const string RESTORE_CLIENT = "RESTORE_CLIENT";
        public const string CREATE_CLIENT_CATEGORIES = "CREATE_CLIENT_CATEGORIES";
        public const string UPDATE_CLIENT_CATEGORIES = "UPDATE_CLIENT_CATEGORIES";
        public const string DELETE_CLIENT_CATEGORIES = "DELETE_CLIENT_CATEGORIES";
        public const string RESTORE_CLIENT_CATEGORIES = "RESTORE_CLIENT_CATEGORIES";
    }

    public static class Articles
    {
        public const string MANAGE_ARTICLES = "MANAGE_ARTICLES";
        public const string VIEW_ARTICLES = "VIEW_ARTICLES";
        public const string CREATE_ARTICLE = "CREATE_ARTICLE";
        public const string UPDATE_ARTICLE = "UPDATE_ARTICLE";
        public const string DELETE_ARTICLE = "DELETE_ARTICLE";
        public const string RESTORE_ARTICLE = "RESTORE_ARTICLE";
        public const string CREATE_ARTICLE_CATEGORIES = "CREATE_ARTICLE_CATEGORIES";
        public const string UPDATE_ARTICLE_CATEGORIES = "UPDATE_ARTICLE_CATEGORIES";
        public const string DELETE_ARTICLE_CATEGORIES = "DELETE_ARTICLE_CATEGORIES";
        public const string RESTORE_ARTICLE_CATEGORIES = "RESTORE_ARTICLE_CATEGORIES";
    }

    public static class Invoices
    {
        public const string MANAGE_INVOICES = "MANAGE_INVOICES";
        public const string VIEW_INVOICES = "VIEW_INVOICES";
        public const string CREATE_INVOICE = "CREATE_INVOICE";
        public const string UPDATE_DRAFT_INVOICE = "UPDATE_DRAFT_INVOICE";
        public const string CANCEL_INVOICE = "CANCEL_INVOICE";
        public const string MARK_INVOICE_PAID = "MARK_INVOICE_PAID";
        public const string DELETE_INVOICE = "DELETE_INVOICE";
        public const string RESTORE_INVOICE = "RESTORE_INVOICE";
    }

    public static class Payments
    {
        public const string MANAGE_PAYMENTS = "MANAGE_PAYMENTS";
        public const string VIEW_PAYMENTS = "VIEW_PAYMENTS";
        public const string RECORD_PAYMENT = "RECORD_PAYMENT";
        public const string CANCEL_PAYMENT = "CANCEL_PAYMENT";
    }

    public static class Stock
    {
        public const string MANAGE_STOCK = "MANAGE_STOCK";
        public const string VIEW_STOCK = "VIEW_STOCK";
        public const string UPDATE_STOCK = "UPDATE_STOCK";
        public const string ADD_ENTRY = "ADD_ENTRY";
    }

    public static class Fournisseurs
    {
        public const string MANAGE_FOURNISSEURS = "MANAGE_FOURNISSEURS";  // was MANAGE_STOCK
        public const string VIEW_FOURNISSEURS = "VIEW_FOURNISSEURS";
        public const string CREATE_FOURNISSEUR = "CREATE_FOURNISSEUR";
        public const string UPDATE_FOURNISSEUR = "UPDATE_FOURNISSEUR";
        public const string DELETE_FOURNISSEUR = "DELETE_FOURNISSEUR";
        public const string RESTORE_FOURNISSEUR = "RESTORE_FOURNISSEUR";
        public const string BLOCK_FOURNISSEUR = "BLOCK_FOURNISSEUR";
        public const string UNBLOCK_FOURNISSEUR = "UNBLOCK_FOURNISSEUR";
    }

    public static class Reports
    {
        public const string MANAGE_REPORTS = "MANAGE_REPORTS";
        public const string VIEW_REPORTS = "VIEW_REPORTS";
        public const string EXPORT_REPORTS = "EXPORT_REPORTS";
    }
}

public static class TenantPrivileges
{
    // Tenant lifecycle
    public const string VIEW_TENANTS = "VIEW_TENANTS";
    public const string CREATE_TENANT = "CREATE_TENANT";
    public const string UPDATE_TENANT = "UPDATE_TENANT";
    public const string DELETE_TENANT = "DELETE_TENANT";
    public const string RESTORE_TENANT = "RESTORE_TENANT";
    public const string SUSPEND_TENANT = "SUSPEND_TENANT";
    public const string ACTIVATE_TENANT = "ACTIVATE_TENANT";

    // Subscription / billing
    public const string MANAGE_SUBSCRIPTIONS = "MANAGE_SUBSCRIPTIONS";
    public const string VIEW_BILLING = "VIEW_BILLING";
}