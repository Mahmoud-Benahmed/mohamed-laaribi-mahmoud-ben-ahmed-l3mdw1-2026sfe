using ERP.AuthService.Domain;
using ERP.AuthService.Domain.Logger;
using ERP.AuthService.Infrastructure.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence
{
    public static class MongoDbInitializer
    {
        public static async Task InitializeAsync(MongoDbContext context, IWebHostEnvironment env, MongoSettings settings)
        {
            if (env.IsDevelopment())
            {
                var rootClient = new MongoClient(settings.RootConnectionString);
                await rootClient.DropDatabaseAsync(settings.DatabaseName);
            }

            var caseInsensitiveCollation =
                new Collation("en", strength: CollationStrength.Secondary);

            #region Controles

            await context.Collection<Controle>(CollectionNames.Controles).Indexes.CreateManyAsync([
                new CreateIndexModel<Controle>(
                    Builders<Controle>.IndexKeys
                        .Ascending(x => x.Libelle),
                    new CreateIndexOptions
                    {
                        Unique = true,
                        Background = true,
                        Collation = caseInsensitiveCollation,
                        Name = "UX_Controle_Libelle"
                    }),

                new CreateIndexModel<Controle>(
                    Builders<Controle>.IndexKeys
                        .Ascending(x => x.Category),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Controle_Category"
                    })
            ]);

            #endregion

            #region Roles

            await context.Collection<Role>(CollectionNames.Roles).Indexes.CreateManyAsync([
                new CreateIndexModel<Role>(
                    Builders<Role>.IndexKeys
                        .Ascending(x => x.Libelle)
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions
                    {
                        Unique = true,
                        Background = true,
                        Collation = caseInsensitiveCollation,
                        Name = "UX_Role_Libelle_TenantId"
                    }),

                new CreateIndexModel<Role>(
                    Builders<Role>.IndexKeys
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Role_TenantId"
                    })
            ]);

            #endregion

            #region AuthUsers

            await context.Collection<AuthUser>(CollectionNames.Users).Indexes.CreateManyAsync([
                // Unique login per tenant
                new CreateIndexModel<AuthUser>(
                    Builders<AuthUser>.IndexKeys
                        .Ascending(x => x.Login)
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions<AuthUser>
                    {
                        Unique = true,
                        Background = true,
                        Collation = caseInsensitiveCollation,
                        PartialFilterExpression =
                            Builders<AuthUser>.Filter.Eq(x => x.IsDeleted, false),
                        Name = "UX_User_Login_TenantId"
                    }),

                // Unique email per tenant
                new CreateIndexModel<AuthUser>(
                    Builders<AuthUser>.IndexKeys
                        .Ascending(x => x.Email)
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions<AuthUser>
                    {
                        Unique = true,
                        Background = true,
                        Collation = caseInsensitiveCollation,
                        PartialFilterExpression =
                            Builders<AuthUser>.Filter.Eq(x => x.IsDeleted, false),
                        Name = "UX_User_Email_TenantId"
                    }),

                // Tenant filtering
                new CreateIndexModel<AuthUser>(
                    Builders<AuthUser>.IndexKeys
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions<AuthUser>
                    {
                        Background = true,
                        Name = "IX_User_TenantId"
                    }),

                // Role filtering
                new CreateIndexModel<AuthUser>(
                    Builders<AuthUser>.IndexKeys
                        .Ascending(x => x.RoleId),
                    new CreateIndexOptions<AuthUser>
                    {
                        Background = true,
                        Name = "IX_User_RoleId"
                    }),

                // Active users
                new CreateIndexModel<AuthUser>(
                    Builders<AuthUser>.IndexKeys
                        .Ascending(x => x.IsActive),
                    new CreateIndexOptions<AuthUser>
                    {
                        Background = true,
                        Name = "IX_User_IsActive"
                    }),

                // Deleted users
                new CreateIndexModel<AuthUser>(
                    Builders<AuthUser>.IndexKeys
                        .Ascending(x => x.IsDeleted),
                    new CreateIndexOptions<AuthUser>
                    {
                        Background = true,
                        Name = "IX_User_IsDeleted"
                    })
            ]);

            #endregion

            #region Privileges

            await context.Collection<Privilege>(CollectionNames.Privileges).Indexes.CreateManyAsync([
                new CreateIndexModel<Privilege>(
                    Builders<Privilege>.IndexKeys
                        .Ascending(x => x.RoleId)
                        .Ascending(x => x.ControleId)
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions<Privilege>
                    {
                        Unique = true,
                        Background = true,
                        Name = "UX_Privilege_Role_Controle_Tenant"
                    }),

                new CreateIndexModel<Privilege>(
                    Builders<Privilege>.IndexKeys
                        .Ascending(x => x.RoleId),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Privilege_RoleId"
                    }),

                new CreateIndexModel<Privilege>(
                    Builders<Privilege>.IndexKeys
                        .Ascending(x => x.ControleId),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Privilege_ControleId"
                    }),

                new CreateIndexModel<Privilege>(
                    Builders<Privilege>.IndexKeys
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Privilege_TenantId"
                    })
            ]);

            #endregion

            #region RefreshTokens

            await context.Collection<RefreshToken>(CollectionNames.RefreshTokens).Indexes.CreateManyAsync([
                new CreateIndexModel<RefreshToken>(
                    Builders<RefreshToken>.IndexKeys
                        .Ascending(x => x.UserId),
                    new CreateIndexOptions<RefreshToken>
                    {
                        Background = true,
                        Name = "IX_RefreshToken_UserId"
                    }),

                new CreateIndexModel<RefreshToken>(
                    Builders<RefreshToken>.IndexKeys
                        .Ascending(x => x.Token),
                    new CreateIndexOptions<RefreshToken>
                    {
                        Unique = true,
                        Background = true,
                        Name = "UX_RefreshToken_Token"
                    }),

                // Automatic cleanup
                new CreateIndexModel<RefreshToken>(
                    Builders<RefreshToken>.IndexKeys
                        .Ascending(x => x.ExpiresAt),
                    new CreateIndexOptions
                    {
                        ExpireAfter = TimeSpan.Zero,
                        Background = true,
                        Name = "TTL_RefreshToken_ExpiresAt"
                    }),

                new CreateIndexModel<RefreshToken>(
                    Builders<RefreshToken>.IndexKeys
                        .Ascending(x => x.TenantId),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_RefreshToken_TenantId"
                    })
            ]);

            #endregion

            #region AuditLogs

            await context.Collection<AuditLog>(CollectionNames.AuditLogs).Indexes.CreateManyAsync([
                // PerformedBy + Timestamp
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys
                        .Ascending(x => x.PerformedBy)
                        .Descending(x => x.Timestamp),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Audit_PerformedBy_Timestamp"
                    }),

                // TargetUserId + Timestamp
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys
                        .Ascending(x => x.TargetUserId)
                        .Descending(x => x.Timestamp),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Audit_TargetUserId_Timestamp"
                    }),

                // TenantId + Timestamp
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Descending(x => x.Timestamp),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Audit_TenantId_Timestamp"
                    }),

                // TenantId + Action + Timestamp
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.Action)
                        .Descending(x => x.Timestamp),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Audit_Tenant_Action_Timestamp"
                    }),

                // Action only
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys
                        .Ascending(x => x.Action),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Audit_Action"
                    }),

                // Timestamp only
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys
                        .Descending(x => x.Timestamp),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "IX_Audit_Timestamp"
                    })
            ]);

            #endregion
        }
    }
}