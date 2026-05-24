using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using ERP.AuthService.Infrastructure.Configuration;
using ERP.AuthService.Infrastructure.Persistence;
using ERP.AuthService.Properties;
using Microsoft.AspNetCore.Identity;

namespace ERP.AuthService.Infrastructure.Persistence.Seeder;

public class GlobalSeeder
{
    private const string emailDomain = AppProperties.AppDomain;
    private const string appName = AppProperties.AppName;

    private const string passwd = $"Admin@{appName}_1234";

    private static readonly List<(string Login, string Email, string FullName, string Password)> SystemAdmins =
    [
        ("sysadmin_1", $"sysadmin1@support.{emailDomain}", "System Admin 1", passwd),
        ("sysadmin_2", $"sysadmin2@support.{emailDomain}", "System Admin 2", passwd),
        ("sysadmin_3", $"sysadmin3@support.{emailDomain}", "System Admin 3", passwd)
    ];

    public static async Task InitializeAsync(
        MongoDbContext context,
        MongoSettings settings,
        IWebHostEnvironment env,
        IServiceProvider services)
    {
        await MongoDbUserInitializer.EnsureAppUserCreatedAsync(settings);

        await MongoDbInitializer.InitializeAsync(context, env, settings);

        using IServiceScope scope = services.CreateScope();

        var userRepository =
            scope.ServiceProvider.GetRequiredService<IAuthUserRepository>();

        var roleRepository =
            scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var privilegeRepository =
            scope.ServiceProvider.GetRequiredService<IPrivilegeRepository>();

        var controleRepository =
            scope.ServiceProvider.GetRequiredService<IControleRepository>();

        var hasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<AuthUser>>();

        // =====================================================
        // 1. CONTROLES
        // =====================================================

        Dictionary<string, Controle> controles =
            await SeedControlesAsync(controleRepository);

        // =====================================================
        // 2. GLOBAL ROLES
        // =====================================================

        Dictionary<string, Role> roles =
            await SeedRolesAsync(roleRepository, tenantId: null);

        // =====================================================
        // 3. GLOBAL PRIVILEGES
        // =====================================================

        await SeedPrivilegesAsync(
            privilegeRepository,
            roles,
            controles,
            tenantId: null);

        // =====================================================
        // 4. GLOBAL USERS
        // =====================================================

        await SeedSystemAdminsAsync(
            userRepository,
            roleRepository,
            hasher);

        // =====================================================
        // 5. TENANT SERVICE USERS
        // =====================================================

        await SeedTenantServiceUsersAsync(
            userRepository,
            roleRepository,
            hasher);
    }

    // =====================================================
    // SYSTEM ADMINS
    // =====================================================

    private static async Task SeedSystemAdminsAsync(
        IAuthUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher<AuthUser> hasher)
    {
        Role adminRole =
            await roleRepository.GetByLibelleAsync(TenantRoles.SUPER_PLATFORM_ADMIN)
            ?? throw new InvalidOperationException(
                "Admin role missing.");

        foreach (var (login, email, fullName, password) in SystemAdmins)
        {
            bool exists =
                await userRepository.ExistsByLoginAsync(login) ||
                await userRepository.ExistsByEmailAsync(email);

            if (exists)
                continue;

            AuthUser user = new(
                login,
                email,
                fullName,
                adminRole.Id,
                tenantId: null);

            user.SetPasswordHash(
                hasher.HashPassword(user, password));

            await userRepository.AddAsync(user);
        }
    }

    // =====================================================
    // TENANT SERVICE USERS
    // =====================================================

    private static async Task SeedTenantServiceUsersAsync(
        IAuthUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher<AuthUser> hasher)
    {
        Role supportRole =
            await roleRepository.GetByLibelleAsync(TenantRoles.TENANT_SUPPORT)
            ?? throw new InvalidOperationException(
                "TENANT_SUPPORT role missing.");

        Role billingRole =
            await roleRepository.GetByLibelleAsync(TenantRoles.BILLING_MANAGER)
            ?? throw new InvalidOperationException(
                "BILLING_MANAGER role missing.");

        Role auditorRole =
            await roleRepository.GetByLibelleAsync(TenantRoles.TENANT_AUDITOR)
            ?? throw new InvalidOperationException(
                "TENANT_AUDITOR role missing.");

        var users = new[]
        {
            (
                Login: "tenant_support",
                Email: $"support@{emailDomain}",
                FullName: "Platform Support",
                Password: $"Support@{appName}_1234",
                RoleId: supportRole.Id
            ),

            (
                Login: "billing_manager",
                Email: $"billing@{emailDomain}",
                FullName: "Billing Manager",
                Password: $"Billing@{appName}_1234",
                RoleId: billingRole.Id
            ),

            (
                Login: "tenant_auditor",
                Email: $"auditor@{emailDomain}",
                FullName: "Tenant Auditor",
                Password: $"Audit@{appName}_1234",
                RoleId: auditorRole.Id
            )
        };

        foreach (var userData in users)
        {
            bool exists =
                await userRepository.ExistsByLoginAsync(userData.Login) ||
                await userRepository.ExistsByEmailAsync(userData.Email);

            if (exists)
                continue;

            AuthUser user = new(
                userData.Login,
                userData.Email,
                userData.FullName,
                userData.RoleId,
                tenantId: null);

            user.SetPasswordHash(
                hasher.HashPassword(user, userData.Password));

            await userRepository.AddAsync(user);
        }
    }

    // =====================================================
    // CONTROLES
    // =====================================================

    private static async Task<Dictionary<string, Controle>> SeedControlesAsync(
        IControleRepository controleRepository)
    {
        Dictionary<string, Controle> result =
            new(StringComparer.OrdinalIgnoreCase);

        var allDefs = PrivilegeRegistry.All
            .Concat(PrivilegeRegistry.TenantsPrivilegeDefinition)
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (PrivilegeDefinition def in allDefs)
        {
            Controle? existing =
                await controleRepository.GetByLibelleAsync(def.Code);

            if (existing != null)
            {
                result[def.Code] = existing;
                continue;
            }

            Controle controle = new(
                def.Category,
                def.Code,
                def.Description);

            await controleRepository.AddAsync(controle);

            result[def.Code] = controle;
        }

        return result;
    }

    // =====================================================
    // ROLES
    // =====================================================

    private static async Task<Dictionary<string, Role>>
    SeedRolesAsync(
        IRoleRepository roleRepository,
        Guid? tenantId)
    {
        Dictionary<string, Role> result =
            new(StringComparer.OrdinalIgnoreCase);

        string[] roleNames =
        [
            TenantRoles.SUPER_PLATFORM_ADMIN,
            TenantRoles.TENANT_SUPPORT,
            TenantRoles.BILLING_MANAGER,
            TenantRoles.TENANT_AUDITOR
        ];

        foreach (string roleName in roleNames)
        {
            Role? existing =
                await roleRepository.GetByLibelleAsync(
                    roleName.ToUpper());

            if (existing != null)
            {
                result[roleName] = existing;
                continue;
            }

            Role role = new(roleName, tenantId);

            await roleRepository.AddAsync(role);

            result[roleName] = role;
        }

        return result;
    }

    // =====================================================
    // PRIVILEGES
    // =====================================================

    private static async Task SeedPrivilegesAsync(
        IPrivilegeRepository privilegeRepository,
        Dictionary<string, Role> roles,
        Dictionary<string, Controle> controles,
        Guid? tenantId)
    {
        var platformRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            TenantRoles.SUPER_PLATFORM_ADMIN,
            TenantRoles.TENANT_SUPPORT,
            TenantRoles.BILLING_MANAGER,
            TenantRoles.TENANT_AUDITOR
        };

        var allDefs = PrivilegeRegistry.All
                            .Concat(PrivilegeRegistry.TenantsPrivilegeDefinition)
                            .ToList();

        foreach ((string roleName, Role role) in roles)
        {
            if (!platformRoles.Contains(roleName))
                continue;
            foreach (PrivilegeDefinition def in allDefs)
            {
                if (!controles.TryGetValue(def.Code, out Controle? controle))
                    continue;

                bool isGranted =
                    RoleHasPrivilege(roleName, def.Code);

                Privilege? existing =
                    await privilegeRepository
                        .GetByRoleIdAndControleIdAsync(
                            role.Id,
                            controle.Id);

                if (existing != null)
                    continue;

                Privilege privilege = new(
                    role.Id,
                    controle.Id,
                    isGranted,
                    tenantId);

                await privilegeRepository.AddAsync(privilege);
            }
        }
    }

    // =====================================================
    // PRIVILEGE RULES
    // =====================================================

    private static bool RoleHasPrivilege(
        string role,
        string code)
    {
        if (role == Roles.SystemAdmin ||
            role == TenantRoles.SUPER_PLATFORM_ADMIN)
            return true;

        return role switch
        {
            TenantRoles.TENANT_SUPPORT =>
                TenantSupportHas(code),

            TenantRoles.BILLING_MANAGER =>
                BillingManagerHas(code),

            TenantRoles.TENANT_AUDITOR =>
                TenantAuditorHas(code),

            Roles.SalesManager =>
                SalesManagerHas(code),

            Roles.StockManager =>
                StockManagerHas(code),

            Roles.Accountant =>
                AccountantHas(code),

            _ => false
        };
    }

    private static bool TenantSupportHas(string code) => code switch
    {
        TenantPrivileges.VIEW_TENANTS => true,
        TenantPrivileges.UPDATE_TENANT => true,
        TenantPrivileges.SUSPEND_TENANT => true,
        TenantPrivileges.ACTIVATE_TENANT => true,

        _ => false
    };

    private static bool BillingManagerHas(string code) => code switch
    {
        TenantPrivileges.MANAGE_SUBSCRIPTIONS => true,
        TenantPrivileges.VIEW_BILLING => true,
        TenantPrivileges.VIEW_TENANTS => true,

        _ => false
    };

    private static bool TenantAuditorHas(string code) => code switch
    {
        TenantPrivileges.VIEW_TENANTS => true,
        TenantPrivileges.VIEW_BILLING => true,

        _ => false
    };

    private static bool SalesManagerHas(string code) => false;

    private static bool StockManagerHas(string code) => false;

    private static bool AccountantHas(string code) => false;
}