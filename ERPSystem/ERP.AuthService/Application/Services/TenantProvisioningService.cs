using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain;
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
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is empty");

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("slug is empty");

        await EnsureIndexesAsync();

        // 1. CONTROLES
        var controles = await SeedControlesAsync();

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
    private async Task<Dictionary<string, Controle>> SeedControlesAsync()
    {
        Dictionary<string, Controle> result = new(StringComparer.OrdinalIgnoreCase);

        var distinctDefs = PrivilegeRegistry.All
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var def in distinctDefs)
        {
            var existing = await _controles.GetByLibelleAsync(def.Code);

            if (existing != null)
            {
                result[def.Code] = existing;
                continue;
            }

            var created = new Controle(def.Category, def.Code, def.Description);
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
    private static bool RoleHasPrivilege(string role, string category, string code)
    {
        if (role == Roles.SystemAdmin)
            return true;

        return role switch
        {
            Roles.SalesManager => SalesManagerHas(code),
            Roles.StockManager => StockManagerHas(code),
            Roles.Accountant => AccountantHas(code),
            _ => false
        };
    }

    private static bool SalesManagerHas(string code) => code switch
    {
        Privileges.Clients.MANAGE_CLIENTS => true,
        Privileges.Clients.VIEW_CLIENTS => true,
        Privileges.Articles.MANAGE_ARTICLES => true,
        Privileges.Invoices.MANAGE_INVOICES => true,
        Privileges.Reports.VIEW_REPORTS => true,
        _ => false
    };

    private static bool StockManagerHas(string code) => code switch
    {
        Privileges.Articles.MANAGE_ARTICLES => true,
        Privileges.Stock.MANAGE_STOCK => true,
        Privileges.Reports.VIEW_REPORTS => true,
        _ => false
    };

    private static bool AccountantHas(string code) => code switch
    {
        Privileges.Clients.VIEW_CLIENTS => true,
        Privileges.Invoices.VIEW_INVOICES => true,
        Privileges.Payments.MANAGE_PAYMENTS => true,
        Privileges.Reports.VIEW_REPORTS => true,
        _ => false
    };

    // =====================================================
    // INDEXES
    // =====================================================
    private async Task EnsureIndexesAsync()
    {
        var users = _db.Collection<AuthUser>("Users");

        var index = Builders<AuthUser>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Email);

        await users.Indexes.CreateOneAsync(new CreateIndexModel<AuthUser>(index));
    }


    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        await _db.Collection<Privilege>("Privileges")
            .DeleteManyAsync(p => p.TenantId == tenantId);
        await _db.Collection<RefreshToken>("RefreshTokens")
            .DeleteManyAsync(p => p.TenantId == tenantId);
        await _db.Collection<AuthUser>("Users")
            .DeleteManyAsync(u => u.TenantId == tenantId);
        await _db.Collection<Role>("Roles")
            .DeleteManyAsync(r => r.TenantId == tenantId);
        await _db.Collection<AuditLog>("AuditLogs")
            .DeleteManyAsync(u => u.TenantId == tenantId);
    }
}