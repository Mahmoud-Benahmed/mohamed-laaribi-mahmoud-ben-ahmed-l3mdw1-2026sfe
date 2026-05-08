namespace ERP.AuthService.Domain.Logger
{

    public enum AuditAction
    {
        // Auth
        Login,
        Logout,
        TokenRefreshed,
        TokenRevoked,

        // Registration
        UserRegistered,

        // Password
        PasswordChanged,
        PasswordChangedByAdmin,

        // Profile
        ProfileUpdated,

        // Account status
        UserActivated,
        UserDeactivated,
        UserDeleted,
        UserRestored,

        // Role
        RoleCreated,
        RoleUpdated,
        RoleDeleted,

        // Controle
        ControleCreated,
        ControleUpdated,
        ControleDeleted,
        // tenant
        TenantAssigned,
        Unauthorized,
        UserNotFound,
        UnhandledError,

        TokenValidated,
        TokenValidationFailed,
    }
}
