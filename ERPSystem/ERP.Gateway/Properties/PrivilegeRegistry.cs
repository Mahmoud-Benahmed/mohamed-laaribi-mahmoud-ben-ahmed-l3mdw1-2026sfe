namespace ERP.Gateway.Properties;

public record PrivilegeDefinition(
    string Code,
    string Category,
    string Description
);

public static class PrivilegeRegistry
{
    public static readonly List<PrivilegeDefinition> All = new()
    {
        // ── Auth
        new(Privileges.Users.MANAGE_USERS,      "AUTH", "Manage users"),
        new(Privileges.Users.VIEW_USERS,        "AUTH", "View users"),
        new(Privileges.Users.CREATE_USER,       "AUTH", "Create user"),
        new(Privileges.Users.UPDATE_USER,       "AUTH", "Update user"),
        new(Privileges.Users.DELETE_USER,       "AUTH", "Delete user"),
        new(Privileges.Users.RESTORE_USER,      "AUTH", "Restore user"),
        new(Privileges.Users.ACTIVATE_USER,     "AUTH", "Activate user"),
        new(Privileges.Users.DEACTIVATE_USER,   "AUTH", "Deactivate user"),
        new(Privileges.Users.ASSIGN_ROLES,      "AUTH", "Assign roles"),
        new(Privileges.Users.CREATE_ROLE,       "AUTH", "Create role"),
        new(Privileges.Users.UPDATE_ROLE,       "AUTH", "Update role"),
        new(Privileges.Users.DELETE_ROLE,       "AUTH", "Delete role"),
        new(Privileges.Users.CREATE_CONTROLE,   "AUTH", "Create controle"),
        new(Privileges.Users.UPDATE_CONTROLE,   "AUTH", "Update controle"),
        new(Privileges.Users.DELETE_CONTROLE,   "AUTH", "Delete controle"),
        new(Privileges.Users.BUY_SUBSCRIPTION,  "AUTH", "Buy subscription for tenant"),
        new(Privileges.Users.EDIT_SYSTEM_SETTINGS,  "AUTH", "Access and change system settings"),
        new(Privileges.Audit.MANAGE_AUDITLOGS,  "AUTH", "Manage audit logs"),

        // ── Clients
        new(Privileges.Clients.MANAGE_CLIENTS,            "CLIENTS", "Manage clients"),
        new(Privileges.Clients.VIEW_CLIENTS,              "CLIENTS", "View clients"),
        new(Privileges.Clients.CREATE_CLIENT,             "CLIENTS", "Create client"),
        new(Privileges.Clients.UPDATE_CLIENT,             "CLIENTS", "Update client"),
        new(Privileges.Clients.DELETE_CLIENT,             "CLIENTS", "Delete client"),
        new(Privileges.Clients.RESTORE_CLIENT,            "CLIENTS", "Restore client"),
        new(Privileges.Clients.CREATE_CLIENT_CATEGORIES,  "CLIENTS", "Create client categories"),
        new(Privileges.Clients.UPDATE_CLIENT_CATEGORIES,  "CLIENTS", "Update client categories"),
        new(Privileges.Clients.DELETE_CLIENT_CATEGORIES,  "CLIENTS", "Delete client categories"),
        new(Privileges.Clients.RESTORE_CLIENT_CATEGORIES, "CLIENTS", "Restore client categories"),

        // ── Articles
        new(Privileges.Articles.MANAGE_ARTICLES,            "ARTICLES", "Manage articles"),
        new(Privileges.Articles.VIEW_ARTICLES,              "ARTICLES", "View articles"),
        new(Privileges.Articles.CREATE_ARTICLE,             "ARTICLES", "Create article"),
        new(Privileges.Articles.UPDATE_ARTICLE,             "ARTICLES", "Update article"),
        new(Privileges.Articles.DELETE_ARTICLE,             "ARTICLES", "Delete article"),
        new(Privileges.Articles.RESTORE_ARTICLE,            "ARTICLES", "Restore article"),
        new(Privileges.Articles.CREATE_ARTICLE_CATEGORIES,  "ARTICLES", "Create article categories"),
        new(Privileges.Articles.UPDATE_ARTICLE_CATEGORIES,  "ARTICLES", "Update article categories"),
        new(Privileges.Articles.DELETE_ARTICLE_CATEGORIES,  "ARTICLES", "Delete article categories"),
        new(Privileges.Articles.RESTORE_ARTICLE_CATEGORIES, "ARTICLES", "Restore article categories"),

        // ── Invoices
        new(Privileges.Invoices.MANAGE_INVOICES,  "FACTURATION", "Manage invoices"),
        new(Privileges.Invoices.VIEW_INVOICES,    "FACTURATION", "View invoices"),
        new(Privileges.Invoices.CREATE_INVOICE,   "FACTURATION", "Create invoice"),
        new(Privileges.Invoices.UPDATE_DRAFT_INVOICE,   "FACTURATION", "Apply Finalize, Add/Remove items on an invoice"),
        new(Privileges.Invoices.CANCEL_INVOICE,   "FACTURATION", "Cancel invoice"),
        new(Privileges.Invoices.MARK_INVOICE_PAID, "FACTURATION", "Mark invoice as paid"),
        new(Privileges.Invoices.DELETE_INVOICE,   "FACTURATION", "Delete invoice"),
        new(Privileges.Invoices.RESTORE_INVOICE,  "FACTURATION", "Restore invoice"),

        // ── Payments
        new(Privileges.Payments.MANAGE_PAYMENTS, "PAIEMENT", "Manage payments"),
        new(Privileges.Payments.VIEW_PAYMENTS,   "PAIEMENT", "View payments"),
        new(Privileges.Payments.RECORD_PAYMENT,  "PAIEMENT", "Record payment"),
        new(Privileges.Payments.CANCEL_PAYMENT,  "PAIEMENT", "Cancel payment"),

        // ── Stock
        new(Privileges.Stock.MANAGE_STOCK, "STOCK", "Manage stock"),
        new(Privileges.Stock.VIEW_STOCK,   "STOCK", "View stock"),
        new(Privileges.Stock.UPDATE_STOCK, "STOCK", "Update stock"),
        new(Privileges.Stock.ADD_ENTRY,    "STOCK", "Add stock entry"),

        // ── Fournisseurs
        new(Privileges.Fournisseurs.MANAGE_FOURNISSEURS,  "FOURNISSEURS", "Manage fournisseurs"),
        new(Privileges.Fournisseurs.VIEW_FOURNISSEURS,    "FOURNISSEURS", "View fournisseurs"),
        new(Privileges.Fournisseurs.CREATE_FOURNISSEUR,   "FOURNISSEURS", "Create fournisseur"),
        new(Privileges.Fournisseurs.UPDATE_FOURNISSEUR,   "FOURNISSEURS", "Update fournisseur"),
        new(Privileges.Fournisseurs.DELETE_FOURNISSEUR,   "FOURNISSEURS", "Delete fournisseur"),
        new(Privileges.Fournisseurs.RESTORE_FOURNISSEUR,  "FOURNISSEURS", "Restore fournisseur"),
        new(Privileges.Fournisseurs.BLOCK_FOURNISSEUR,    "FOURNISSEURS", "Block fournisseur"),
        new(Privileges.Fournisseurs.UNBLOCK_FOURNISSEUR,  "FOURNISSEURS", "Unblock fournisseur"),

        // ── Reports
        new(Privileges.Reports.MANAGE_REPORTS,  "REPORTING", "Manage reports"),
        new(Privileges.Reports.VIEW_REPORTS,    "REPORTING", "View reports"),
        new(Privileges.Reports.EXPORT_REPORTS,  "REPORTING", "Export reports")
    };

    public static readonly List<PrivilegeDefinition> TenantsPrivilegeDefinition = new()
    {

        new(TenantPrivileges.VIEW_TENANTS, "TENANT", "View tenants"),
        new(TenantPrivileges.CREATE_TENANT, "TENANT", "Create tenant"),
        new(TenantPrivileges.UPDATE_TENANT, "TENANT", "Update tenant"),
        new(TenantPrivileges.DELETE_TENANT, "TENANT", "Delete tenant"),
        new(TenantPrivileges.RESTORE_TENANT, "TENANT", "Restore tenant"),
        new(TenantPrivileges.SUSPEND_TENANT, "TENANT", "Suspend tenant"),
        new(TenantPrivileges.ACTIVATE_TENANT, "TENANT", "Activate tenant"),

        // ── Subscription / Billing
        new(TenantPrivileges.MANAGE_SUBSCRIPTIONS, "TENANT", "Manage subscriptions"),
        new(TenantPrivileges.VIEW_BILLING, "TENANT", "View billing")
    };

    // In PrivilegeRegistry, after the list definition:
    static PrivilegeRegistry()
    {
        List<string> duplicates = All
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"PrivilegeRegistry has duplicate codes: {string.Join(", ", duplicates)}");
    }
}