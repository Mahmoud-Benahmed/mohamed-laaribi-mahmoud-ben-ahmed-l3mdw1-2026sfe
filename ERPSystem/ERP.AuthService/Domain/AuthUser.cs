using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace ERP.AuthService.Domain;

public class AuthUser
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; private set; }

    [BsonRequired]
    public string Login { get; private set; }

    public string Email { get; set; } = default!;

    public string FullName { get; set; }

    public string PasswordHash { get; set; } = default!;

    public bool MustChangePassword { get; set; } = true;

    [BsonElement("Settings")]
    public UserSettings Settings { get; set; } = new UserSettings
    {
        Theme = Theme.light,
        Language = Language.en
    };

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid TenantId { get; private set; }  // Nullable ✅

    public bool IsActive { get; private set; } = true; // Login control
    public bool IsDeleted { get; private set; } = false; // alternative to hard delete: in case the instance has related records


    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid RoleId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public DateTime? LastLoginAt { get; private set; }



    private AuthUser() { }

    public AuthUser(string login, string email, string fullName, Guid roleId, Guid tenantId, UserSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentNullException("FullName is required");

        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Username is required");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");

        Id = Guid.NewGuid();
        Email = email;
        Login = login;
        FullName = fullName;
        RoleId = roleId;
        TenantId= tenantId;
        Settings = settings ?? new UserSettings { Theme = Theme.light, Language = Language.en };
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateSettings(string theme, string language)
    {
        Settings.Theme = Enum.TryParse<Theme>(theme, true, out Theme t) ? t : Theme.light;
        Settings.Language = Enum.TryParse<Language>(language, true, out Language l) ? l : Language.en;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullname, string email)
    {
        if (string.IsNullOrWhiteSpace(fullname))
            throw new ArgumentException("FullName is required");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");

        FullName = fullname;
        Email = email;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required");
        PasswordHash = passwordHash;
    }

    public void SetRole(Guid roleId)
    {
        RoleId = roleId;
        UpdatedAt = DateTime.UtcNow;
    }
    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;// enfore immediate Deactivating to prevent deleted user from logging-in but the inverse is not correct: Recover doesn't activate the deleted user in case he is deactivated (can be activated by Activate)
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;// if user deactivated by the system, it can be activated by Activate() which not set here to prevent accidental activation for a deactivated user
        UpdatedAt = DateTime.UtcNow;
    }

    public void ForcePasswordChange()
    {
        MustChangePassword = true;
    }

    public bool HasLoggedInBefore() => LastLoginAt != null;


    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required");

        PasswordHash = newPasswordHash;

        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanLogin()
    {
        return IsActive && !IsDeleted;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}

public class UserSettings
{
    [BsonElement("Theme")]
    [BsonRepresentation(BsonType.String)] // store as string, not int
    public Theme Theme { get; set; } = Theme.light;

    [BsonElement("Language")]
    [BsonRepresentation(BsonType.String)] // store as string, not int
    public Language Language { get; set; } = Language.en;
}


public enum Theme
{
    light,
    dark
}

public enum Language
{
    en,
    fr
}