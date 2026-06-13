using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain;
using ERP.AuthService.Domain.Cache;
using ERP.AuthService.Domain.Logger;
using ERP.AuthService.Infrastructure.Persistence;
using ERP.AuthService.Properties;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly MongoDbContext _db;
    private readonly IAuthUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IControleRepository _controles;
    private readonly IPrivilegeRepository _privileges;
    private readonly IPasswordHasher<AuthUser> _hasher;

    public TenantProvisioningService(
        MongoDbContext db,
        IAuthUserRepository users,
        IRoleRepository roles,
        IControleRepository controles,
        IPrivilegeRepository privileges,
        IPasswordHasher<AuthUser> hasher)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _controles = controles;
        _privileges = privileges;
        _hasher = hasher;
    }

    public async Task ProvisionAsync(Guid tenantId, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("slug is empty");

        await EnsureIndexesAsync();

        // 1. CONTROLES
        var controles = await SeedControlesAsync(tenantId);

        // 2. ROLES
        var roles = await SeedRolesAsync(tenantId);

        // 3. PRIVILEGES
        await SeedPrivilegesAsync(tenantId, roles, controles);

        // 4. USERS (tenant)
        await SeedTenantUsersAsync(tenantId, slug);
    }

    // =====================================================
    // CONTROLES
    // =====================================================
    private async Task<Dictionary<string, Controle>> SeedControlesAsync(Guid tenantId)
    {
        Dictionary<string, Controle> result = new(StringComparer.OrdinalIgnoreCase);

        var distinctDefs = PrivilegeRegistry.All
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        // Load all existing controles for THIS tenant in one query
        var existingForTenant = await _db.Collection<Controle>("Controles")
            .Find(Builders<Controle>.Filter.Eq(c => c.TenantId, tenantId))
            .ToListAsync();

        var existingByLibelle = existingForTenant
            .ToDictionary(c => c.Libelle, c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var def in distinctDefs)
        {
            if (existingByLibelle.TryGetValue(def.Code, out var existing))
            {
                result[def.Code] = existing;
                continue;
            }

            var created = new Controle(def.Category, def.Code, def.Description, tenantId);
            await _controles.AddAsync(created);
            result[def.Code] = created;
        }

        return result;
    }

    // =====================================================
    // ROLES
    // =====================================================
    private async Task<Dictionary<string, Role>> SeedRolesAsync(Guid? tenantId)
    {
        Dictionary<string, Role> result = new(StringComparer.OrdinalIgnoreCase);

        string[] roleNames =
        [
            Roles.SystemAdmin,
            Roles.Accountant,
            Roles.SalesManager,
            Roles.StockManager
        ];

        var existingRoles = await _db.Collection<Role>("Roles")
            .Find(Builders<Role>.Filter.Eq(r => r.TenantId, tenantId))
            .ToListAsync();

        var existingByName = existingRoles
            .ToDictionary(r => r.Libelle, r => r, StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames)
        {
            if (existingByName.TryGetValue(roleName.ToUpper(), out var existing))
            {
                result[roleName] = existing;
                continue;
            }

            var role = new Role(roleName, tenantId);
            await _roles.AddAsync(role);
            result[roleName] = role;
        }

        return result;
    }

    // =====================================================
    // PRIVILEGES
    // =====================================================
    private async Task SeedPrivilegesAsync(
        Guid? tenantId,
        Dictionary<string, Role> roles,
        Dictionary<string, Controle> controles)
    {
        foreach (var (roleName, role) in roles)
        {
            foreach (var def in PrivilegeRegistry.All)
            {
                if (!controles.TryGetValue(def.Code, out var controle))
                    continue;

                bool isGranted = RoleHasPrivilege(roleName, def.Category, def.Code);

                var existing =
                    await _privileges.GetByRoleIdAndControleIdAsync(role.Id, controle.Id);

                if (existing != null)
                    continue;

                var privilege = new Privilege(role.Id, controle.Id, isGranted, tenantId);
                await _privileges.AddAsync(privilege);
            }
        }
    }

    // =====================================================
    // TENANT USERS
    // =====================================================
    private async Task SeedTenantUsersAsync(Guid tenantId, string slug)
    {
        var rolesFilter = Builders<Role>.Filter.Eq(r => r.TenantId, tenantId);
        var roles = await _db.Collection<Role>("Roles")
            .Find(rolesFilter)
            .ToListAsync();

        var adminRole = roles.First(r => r.Libelle == Roles.SystemAdmin.ToUpper());
        var salesRole = roles.First(r => r.Libelle == Roles.SalesManager.ToUpper());
        var stockRole = roles.First(r => r.Libelle == Roles.StockManager.ToUpper());
        var accountRole = roles.First(r => r.Libelle == Roles.Accountant.ToUpper());

        var seedUsers = new[]
        {
            ($"admin_{slug}", $"admin@{slug}.{AppProperties.AppDomain}", "Admin Benson", $"Admin@{slug}_1234", adminRole.Id),
            ($"sales_{slug}", $"sales@{slug}.{AppProperties.AppDomain}", "Sales Pops", $"Sales@{slug}_1234", salesRole.Id),
            ($"stock_{slug}", $"stock@{slug}.{AppProperties.AppDomain}", "Stock Skips", $"Stock@{slug}_1234", stockRole.Id),
            ($"account_{slug}", $"account@{slug}.{AppProperties.AppDomain}", "Accountant Don", $"Account@{slug}_1234", accountRole.Id),
        };

        foreach (var (login, email, fullName, password, roleId) in seedUsers)
        {
            bool exists =
                await _users.ExistsByEmailAsync(email) ||
                await _users.ExistsByLoginAsync(login);

            if (exists)
                continue;

            var user = new AuthUser(
                login,
                email,
                fullName,
                roleId,
                tenantId,
                new UserSettings
                {
                    Theme = Enum.GetValues<Theme>().OrderBy(_ => Guid.NewGuid()).First(),
                    Language = Enum.GetValues<Language>().OrderBy(_ => Guid.NewGuid()).First()
                });

            user.SetPasswordHash(_hasher.HashPassword(user, password));

            await _users.AddAsync(user);
        }
    }

    // =====================================================
    // PRIVILEGE RULES (UNCHANGED)
    // =====================================================
    // =====================================================
    // PRIVILEGE RULES (ALIGNED WITH ACADEMIC REPORT)
    // =====================================================

    private static readonly HashSet<string> SalesManagerPrivileges = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Clients (Full Lifecycle except Deletion per business constraints)
        Privileges.Clients.MANAGE_CLIENTS,
        Privileges.Clients.VIEW_CLIENTS,
        Privileges.Clients.CREATE_CLIENT,
        Privileges.Clients.UPDATE_CLIENT,
        Privileges.Clients.CREATE_CLIENT_CATEGORIES,
        Privileges.Clients.UPDATE_CLIENT_CATEGORIES,

        // ── Articles / Catalog (Creation & Modification access, no Deletion)
        Privileges.Articles.VIEW_ARTICLES,
        Privileges.Articles.CREATE_ARTICLE,
        Privileges.Articles.UPDATE_ARTICLE,
        Privileges.Articles.CREATE_ARTICLE_CATEGORIES,
        Privileges.Articles.UPDATE_ARTICLE_CATEGORIES,

        // ── Invoices / Sales Flow (Pipeline engagement, no core data drops)
        Privileges.Invoices.MANAGE_INVOICES,
        Privileges.Invoices.VIEW_INVOICES,
        Privileges.Invoices.CREATE_INVOICE,
        Privileges.Invoices.UPDATE_DRAFT_INVOICE,
        Privileges.Invoices.CANCEL_INVOICE,

        // ── Read-only Consultation Context boundaries
        Privileges.Payments.VIEW_PAYMENTS,
        Privileges.Stock.VIEW_STOCK,
        Privileges.Reports.VIEW_REPORTS,
        Privileges.Reports.EXPORT_REPORTS
    };

    private static readonly HashSet<string> StockManagerPrivileges = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Articles Catalog (Complete CRUD lifecycle authority)
        Privileges.Articles.MANAGE_ARTICLES,
        Privileges.Articles.VIEW_ARTICLES,
        Privileges.Articles.CREATE_ARTICLE,
        Privileges.Articles.UPDATE_ARTICLE,
        Privileges.Articles.DELETE_ARTICLE,
        Privileges.Articles.RESTORE_ARTICLE,
        Privileges.Articles.CREATE_ARTICLE_CATEGORIES,
        Privileges.Articles.UPDATE_ARTICLE_CATEGORIES,
        Privileges.Articles.DELETE_ARTICLE_CATEGORIES,
        Privileges.Articles.RESTORE_ARTICLE_CATEGORIES,

        // ── Stock Tracking & Logistics Data
        Privileges.Stock.MANAGE_STOCK,
        Privileges.Stock.VIEW_STOCK,
        Privileges.Stock.UPDATE_STOCK,
        Privileges.Stock.ADD_ENTRY,

        // ── Fournisseurs / Procurement (Full CRUD lifecycle control)
        Privileges.Fournisseurs.MANAGE_FOURNISSEURS,
        Privileges.Fournisseurs.VIEW_FOURNISSEURS,
        Privileges.Fournisseurs.CREATE_FOURNISSEUR,
        Privileges.Fournisseurs.UPDATE_FOURNISSEUR,
        Privileges.Fournisseurs.DELETE_FOURNISSEUR,
        Privileges.Fournisseurs.RESTORE_FOURNISSEUR,
        Privileges.Fournisseurs.BLOCK_FOURNISSEUR,
        Privileges.Fournisseurs.UNBLOCK_FOURNISSEUR,

        // ── General Operational Context Updates
        Privileges.Reports.VIEW_REPORTS
    };

    private static readonly HashSet<string> AccountantPrivileges = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Payments & Financial Operations (Full Transactional Ownership)
        Privileges.Payments.MANAGE_PAYMENTS,
        Privileges.Payments.VIEW_PAYMENTS,
        Privileges.Payments.RECORD_PAYMENT,
        Privileges.Payments.CANCEL_PAYMENT,

        // ── Invoices Validation Lifecycle 
        Privileges.Invoices.MANAGE_INVOICES,
        Privileges.Invoices.VIEW_INVOICES,
        Privileges.Invoices.MARK_INVOICE_PAID,

        // ── Audit Ledger Consultations (Cross-service visibility, zero data alteration)
        Privileges.Clients.VIEW_CLIENTS,
        Privileges.Articles.VIEW_ARTICLES,
        Privileges.Stock.VIEW_STOCK,
        Privileges.Fournisseurs.VIEW_FOURNISSEURS,

        // ── Financial Reporting
        Privileges.Reports.MANAGE_REPORTS,
        Privileges.Reports.VIEW_REPORTS,
        Privileges.Reports.EXPORT_REPORTS
    };

    private static bool RoleHasPrivilege(string role, string category, string code)
    {
        // Base case-insensitive short-circuit check for absolute Admin control
        if (string.Equals(role, Roles.SystemAdmin, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(role, Roles.SalesManager, StringComparison.OrdinalIgnoreCase))
            return SalesManagerPrivileges.Contains(code);

        if (string.Equals(role, Roles.StockManager, StringComparison.OrdinalIgnoreCase))
            return StockManagerPrivileges.Contains(code);

        if (string.Equals(role, Roles.Accountant, StringComparison.OrdinalIgnoreCase))
            return AccountantPrivileges.Contains(code);

        return false;
    }

    // =====================================================
    // INDEXES
    // =====================================================
    private async Task EnsureIndexesAsync()
    {
        var users = _db.Collection<AuthUser>("Users");

        await users.Indexes.CreateManyAsync([
            new CreateIndexModel<AuthUser>(
                Builders<AuthUser>.IndexKeys.Ascending(u => u.TenantId).Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "idx_tenant_email" }),
            new CreateIndexModel<AuthUser>(
                Builders<AuthUser>.IndexKeys.Ascending(u => u.TenantId).Ascending(u => u.Login),
                new CreateIndexOptions { Unique = true, Name = "idx_tenant_login" }),
        ]);
    }


    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        await _db.Collection<TenantCache>("TenantsCache")
     .DeleteManyAsync(t => t.Id == tenantId);

        await _db.Collection<Privilege>("Privileges")
            .DeleteManyAsync(p => p.TenantId == tenantId);

        await _db.Collection<RefreshToken>("RefreshTokens")
            .DeleteManyAsync(r => r.TenantId == tenantId);

        await _db.Collection<AuthUser>("Users")
            .DeleteManyAsync(u => u.TenantId == tenantId);

        await _db.Collection<Role>("Roles")
            .DeleteManyAsync(r => r.TenantId == tenantId);

        await _db.Collection<AuditLog>("AuditLogs")
            .DeleteManyAsync(u => u.TenantId == tenantId);
    }
}