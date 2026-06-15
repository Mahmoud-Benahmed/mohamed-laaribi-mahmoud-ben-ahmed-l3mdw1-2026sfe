/////////////////////////////////////////////////////////////
/// THIS FILE IS NOT USED 
/// AND LEFT FOR REFERENCE PURPOSES TO 
/// IMPLEMENT BETTER SEEDERS BASED ON IT 
/// SO ❌ DON'T DELETE IT
/////////////////////////////////////////////////////////////

using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Services;
using ERP.AuthService.Domain;
using ERP.AuthService.Infrastructure.Persistence;
using ERP.AuthService.Properties;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace ERPrivileges.AuthService.Infrastructure.Persistence.Seeder
{

    public static class AuthServiceSeeder
    {
        private static readonly Guid _tenantId;
        private static readonly List<(string Login, string Email, string FullName, string Password)> SystemAdmins =
        [
            ("sysadmin_1", "sysadmin1@support.erp.com", "System Admin 1", "Admin@1234"),
            ("sysadmin_2", "sysadmin2@support.erp.com", "System Admin 2", "Admin@1234"),
            ("sysadmin_3", "sysadmin3@support.erp.com", "System Admin 3", "Admin@1234")
        ];


        public static async Task SeedAsync(
            MongoDbContext dbContext,
            IAuditLogRepository auditLogRepository,
            IAuthUserRepository userRepository,
            IRoleRepository roleRepository,
            IControleRepository controleRepository,
            IPrivilegeRepository privilegeRepository,
            IPasswordHasher<AuthUser> passwordHasher,
            IConfiguration configuration,
            Guid? tenantId,
            IHostEnvironment env)
        {
            bool isTenantSeed = tenantId.HasValue;

            // controles are shared — always seed them regardless of tenant
            Dictionary<string, Controle> controles = await SeedControlesAsync(controleRepository);

            Dictionary<string, Role> roles = await SeedRolesAsync(roleRepository, tenantId);
            await SeedPrivilegesAsync(privilegeRepository, roles, controles, tenantId);

            if (isTenantSeed)
                // provision default tenant users
                await SeedUsersAsync(userRepository, roleRepository, configuration, passwordHasher, tenantId);
            else
                // system startup — seed global SuperAdmins only
                await SeedSystemAdminsAsync(userRepository, passwordHasher, roleRepository);
        }

        // ── 1. SEED CONTROLES ─────────────────────────────
        private static async Task<Dictionary<string, Controle>> SeedControlesAsync(
            IControleRepository controleRepository)
        {
            Dictionary<string, Controle> result = new(StringComparer.OrdinalIgnoreCase);

            IEnumerable<PrivilegeDefinition> distinctDefs = PrivilegeRegistry.All
                .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

            foreach (PrivilegeDefinition def in distinctDefs)
            {
                Controle? existing = await controleRepository.GetByLibelleAsync(def.Code);

                if (existing is not null)
                {
                    result[def.Code] = existing;
                    continue;
                }

                // ← was missing: actually insert when not found
                Controle controle = new Controle(def.Category, def.Code, def.Description);
                await controleRepository.AddAsync(controle);
                result[def.Code] = controle;
            }

            return result;
        }

        // ── 2. SEED ROLES ─────────────────────────────────
        private static async Task<Dictionary<string, Role>> SeedRolesAsync(
            IRoleRepository roleRepository,
            Guid? tenantId)
        {
            Dictionary<string, Role> result = new(StringComparer.OrdinalIgnoreCase);
            string[] roleNames = [Roles.SystemAdmin, Roles.Accountant, Roles.SalesManager, Roles.StockManager];

            foreach (string roleName in roleNames)
            {
                try
                {
                    Role role = new Role(roleName, tenantId);
                    await roleRepository.AddAsync(role);
                    result[roleName] = role;
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
                {
                    Role? existing = await roleRepository.GetByLibelleAsync(roleName.ToUpper());
                    result[roleName] = existing!;
                }
            }

            return result;
        }

        // ── 3. SEED PRIVILEGES ────────────────────────────
        private static async Task SeedPrivilegesAsync(
            IPrivilegeRepository privilegeRepository,
            Dictionary<string, Role> roles,
            Dictionary<string, Controle> controles,
            Guid? tenantId)
        {
            foreach ((string roleName, Role role) in roles)
            {
                foreach (PrivilegeDefinition def in PrivilegeRegistry.All)
                {
                    if (!controles.TryGetValue(def.Code, out Controle? controle))
                        continue;

                    bool isGranted = RoleHasPrivilege(roleName, def.Category, def.Code);

                    Privilege? existing = await privilegeRepository
                        .GetByRoleIdAndControleIdAsync(role.Id, controle.Id);

                    if (existing is null)
                    {
                        Privilege privilege = new Privilege(role.Id, controle.Id, isGranted, tenantId);
                        await privilegeRepository.AddAsync(privilege);
                    }
                }
            }
        }

        // ── 4. SEED TENANT USERS ──────────────────────────
        private static async Task SeedUsersAsync(
            IAuthUserRepository userRepository,
            IRoleRepository roleRepository,
            IConfiguration configuration,
            IPasswordHasher<AuthUser> passwordHasher,
            Guid? tenantId)
        {
            List<Role> roles = await roleRepository.GetAllAsync();

            Role adminRole = roles.Find(r => r.Libelle == Roles.SystemAdmin) ?? throw new InvalidOperationException($"Role '{Roles.SystemAdmin}' not found.");
            Role salesRole = roles.Find(r => r.Libelle == Roles.SalesManager) ?? throw new InvalidOperationException($"Role '{Roles.SalesManager}' not found.");
            Role stockRole = roles.Find(r => r.Libelle == Roles.StockManager) ?? throw new InvalidOperationException($"Role '{Roles.StockManager}' not found.");
            Role accountRole = roles.Find(r => r.Libelle == Roles.Accountant) ?? throw new InvalidOperationException($"Role '{Roles.Accountant}' not found.");

            var seedUsers = new List<(string Login, string Email, string FullName, string Password, Guid RoleId)>
    {
        ("admin_erp1234",   "admin@erp.com",    "John DOE",        "Admin@1234",   adminRole.Id),
        ("sales_erp1234",   "sales@erp.com",    "Sales Alex",      "Sales@1234",   salesRole.Id),
        ("stock_erp1234",   "stock@erp.com",    "Stock David",     "Stock@1234",   stockRole.Id),
        ("account_erp1234", "account@erp.com",  "Accountant Jane", "Account@1234", accountRole.Id),
    };

            Random random = new Random();  // ← outside the loop
            Theme[] themes = Enum.GetValues<Theme>();
            Language[] languages = Enum.GetValues<Language>();

            foreach ((string login, string email, string fullName, string password, Guid roleId) in seedUsers)
            {
                bool exists = await userRepository.ExistsByEmailAsync(email)
                           || await userRepository.ExistsByLoginAsync(login);
                if (exists)
                    continue;

                UserSettings settings = new UserSettings
                {
                    Theme = themes[random.Next(themes.Length)],
                    Language = languages[random.Next(languages.Length)]
                };

                AuthUser user = new AuthUser(login, email, fullName, roleId, tenantId, settings);
                user.SetPasswordHash(passwordHasher.HashPassword(user, password));
                await userRepository.AddAsync(user);
            }
        }

        // ── 5. SEED GLOBAL SYSTEM ADMINS ─────────────────
        private static async Task SeedSystemAdminsAsync(
            IAuthUserRepository userRepository,
            IPasswordHasher<AuthUser> passwordHasher,
            IRoleRepository roleRepository)
        {
            Role adminRole = await roleRepository.GetByLibelleAsync(Roles.SystemAdmin)
                ?? throw new InvalidOperationException("SystemAdmin role missing.");

            foreach ((string login, string email, string fullName, string password) in SystemAdmins)
            {
                bool exists = await userRepository.ExistsByLoginAsync(login)
                           || await userRepository.ExistsByEmailAsync(email);
                if (exists)
                    continue;

                AuthUser user = new AuthUser(login, email, fullName, adminRole.Id, tenantId: null);
                user.SetPasswordHash(passwordHasher.HashPassword(user, password));
                await userRepository.AddAsync(user);
            }
        }



        /////////////////////////////
        /////// Private Helpers
        ////////////////////////////
        private static bool RoleHasPrivilege(string role, string category, string privilegeCode)
        {
            if (role == Roles.SystemAdmin) return true;

            return role switch
            {
                Roles.SalesManager => SalesManagerHas(privilegeCode),
                Roles.StockManager => StockManagerHas(privilegeCode),
                Roles.Accountant => AccountantHas(privilegeCode),
                _ => false
            };
        }

        private static bool SalesManagerHas(string code) => code switch
        {
            // Clients — full
            Privileges.Clients.MANAGE_CLIENTS => true,
            Privileges.Clients.VIEW_CLIENTS => true,
            Privileges.Clients.CREATE_CLIENT => true,
            Privileges.Clients.UPDATE_CLIENT => true,
            Privileges.Clients.DELETE_CLIENT => true,
            Privileges.Clients.CREATE_CLIENT_CATEGORIES => true,
            Privileges.Clients.UPDATE_CLIENT_CATEGORIES => true,
            Privileges.Clients.DELETE_CLIENT_CATEGORIES => true,

            // Articles — create/update/view only (no delete/restore/categories)
            Privileges.Articles.MANAGE_ARTICLES => true,
            Privileges.Articles.VIEW_ARTICLES => true,
            Privileges.Articles.CREATE_ARTICLE => true,
            Privileges.Articles.UPDATE_ARTICLE => true,

            // Fournisseurs — view only
            Privileges.Fournisseurs.VIEW_FOURNISSEURS => true,

            // Invoices — create/view only
            Privileges.Invoices.MANAGE_INVOICES => true,
            Privileges.Invoices.VIEW_INVOICES => true,
            Privileges.Invoices.CREATE_INVOICE => true,
            Privileges.Invoices.UPDATE_DRAFT_INVOICE => true,
            Privileges.Invoices.MARK_INVOICE_PAID => true,
            Privileges.Invoices.CANCEL_INVOICE => true,
            Privileges.Invoices.DELETE_INVOICE => true,

            // Payments — view only
            Privileges.Payments.VIEW_PAYMENTS => true,

            // Stock — read only
            Privileges.Stock.VIEW_STOCK => true,

            // Reports — view + export
            Privileges.Reports.VIEW_REPORTS => true,
            Privileges.Reports.EXPORT_REPORTS => true,

            _ => false
        };

        private static bool StockManagerHas(string code) => code switch
        {
            // Articles — full
            Privileges.Articles.MANAGE_ARTICLES => true,
            Privileges.Articles.VIEW_ARTICLES => true,
            Privileges.Articles.CREATE_ARTICLE => true,
            Privileges.Articles.UPDATE_ARTICLE => true,
            Privileges.Articles.DELETE_ARTICLE => true,
            Privileges.Articles.CREATE_ARTICLE_CATEGORIES => true,
            Privileges.Articles.UPDATE_ARTICLE_CATEGORIES => true,
            Privileges.Articles.DELETE_ARTICLE_CATEGORIES => true,

            // Stock — full
            Privileges.Stock.MANAGE_STOCK => true,
            Privileges.Stock.VIEW_STOCK => true,
            Privileges.Stock.UPDATE_STOCK => true,
            Privileges.Stock.ADD_ENTRY => true,

            Privileges.Fournisseurs.VIEW_FOURNISSEURS => true,
            Privileges.Fournisseurs.CREATE_FOURNISSEUR => true,
            Privileges.Fournisseurs.UPDATE_FOURNISSEUR => true,
            Privileges.Fournisseurs.DELETE_FOURNISSEUR => true,
            Privileges.Fournisseurs.BLOCK_FOURNISSEUR => true,
            Privileges.Fournisseurs.UNBLOCK_FOURNISSEUR => true,

            // Reports — view only
            Privileges.Reports.VIEW_REPORTS => true,

            _ => false
        };

        private static bool AccountantHas(string code) => code switch
        {
            // Clients — view only
            Privileges.Clients.VIEW_CLIENTS => true,

            // Articles - view only
            Privileges.Articles.VIEW_ARTICLES => true,

            Privileges.Stock.VIEW_STOCK => true,


            // Invoices — view + validate only
            Privileges.Invoices.MANAGE_INVOICES => true,
            Privileges.Invoices.VIEW_INVOICES => true,

            // Payments — full
            Privileges.Payments.MANAGE_PAYMENTS => true,
            Privileges.Payments.VIEW_PAYMENTS => true,
            Privileges.Payments.RECORD_PAYMENT => true,

            // Reports — full
            Privileges.Reports.MANAGE_REPORTS => true,
            Privileges.Reports.VIEW_REPORTS => true,
            Privileges.Reports.EXPORT_REPORTS => true,

            _ => false
        };
    }
}