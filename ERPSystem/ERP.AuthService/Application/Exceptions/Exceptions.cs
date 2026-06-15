using ERP.AuthService.Properties;

namespace ERP.AuthService.Application.Exceptions;

public class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException() : base("Email already exists.")
    {
    }
    public EmailAlreadyExistsException(string message) : base(message)
    {
    }
    public EmailAlreadyExistsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class SubscriptionPlanNotFoundException() : Exception($"SubscriptionPlan was not found.");

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid credentials.")
    {
    }
    public InvalidCredentialsException(string message) : base(message)
    {
    }
    public InvalidCredentialsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class LoginAlreadyExsistException : Exception
{
    public LoginAlreadyExsistException() : base("Login already exists.")
    {
    }
    public LoginAlreadyExsistException(string message) : base(message)
    {
    }
    public LoginAlreadyExsistException(string message, Exception innerException) : base(message, innerException)
    {
    }
}


public class TokenAlreadyRevokedException : Exception
{
    public TokenAlreadyRevokedException() : base("Token already revoked.")
    {
    }
    public TokenAlreadyRevokedException(string message) : base(message)
    {
    }
    public TokenAlreadyRevokedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class UnauthorizedOperationException : Exception
{
    public UnauthorizedOperationException(string message) : base(message) { }
}

public class UserActiveException : UnauthorizedAccessException
{
    public UserActiveException() : base("User already active")
    {
    }
    public UserActiveException(string message) : base(message)
    {
    }
}

public class UserInactiveException : Exception
{
    public UserInactiveException() : base("User is not active.")
    {
    }
    public UserInactiveException(string message) : base(message)
    {
    }
    public UserInactiveException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class UserNotFoundException : Exception
{
    public UserNotFoundException(string login)
        : base($"User with login '{login}' was not found.")
    {
    }

    public UserNotFoundException(Guid id)
        : base($"User with id '{id}' was not found.") { }
}

public class ControleNotFoundException : Exception
{
    public ControleNotFoundException(Guid id) : base($"Controle with id '{id}' was not found.") { }
    public ControleNotFoundException(string message) : base(message) { }
}
public class ControleAlreadyExistException : Exception
{
    public ControleAlreadyExistException(Guid id) : base($"A duplicate Controle with id '{id}' was found.") { }
    public ControleAlreadyExistException(string message) : base(message) { }
}

public class PrivilegeNotFoundException : Exception
{
    public PrivilegeNotFoundException(Guid roleId, Guid controleId) : base($"Privilege  with RoleId '{roleId}' & ControleId '{controleId}' was not found.") { }
    public PrivilegeNotFoundException(Guid id) : base($"Privilege with id '{id}' was not found.") { }
    public PrivilegeNotFoundException(string message) : base(message) { }
}

public class PrivilegeAlreadyExistException : Exception
{
    public PrivilegeAlreadyExistException(Guid roleId, Guid controleId) : base($"A duplicate Privilege with RoleId '{roleId}' & ControleId '{controleId}' was found.") { }
    public PrivilegeAlreadyExistException(Guid id) : base($"A duplicate Privilege with id '{id}' was found.") { }
    public PrivilegeAlreadyExistException(string message) : base(message) { }
}
public class RoleNotFoundException : Exception
{
    public RoleNotFoundException(Guid id) : base($"Role with id '{id}' was not found.") { }
    public RoleNotFoundException(string message) : base(message) { }
}

public class RoleAlreadyExistException : Exception
{
    public RoleAlreadyExistException(Guid id) : base($"A duplicate role with id '{id}' was found.") { }
    public RoleAlreadyExistException(string libelle) : base($"A duplicate role with Libelle '{libelle}' was found.") { }
}

public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException()
        : base("Invalid refresh token.") { }
}

public class TenantUserLimitReachedException : Exception
{
    public TenantUserLimitReachedException()
        : base("Tenant user limit reached.") { }
}

public class DuplicateKeyException(string key) : Exception(key);