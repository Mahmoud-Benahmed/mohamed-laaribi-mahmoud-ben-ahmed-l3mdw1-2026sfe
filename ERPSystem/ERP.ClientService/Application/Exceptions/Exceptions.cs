namespace ERP.ClientService.Application.Exceptions;

public sealed class ClientNotFoundException : KeyNotFoundException
{
    public ClientNotFoundException(Guid id)
        : base($"Client with id '{id}' was not found.") { }

    public ClientNotFoundException(string email)
        : base($"Client with email '{email}' was not found.") { }
}

public sealed class ClientAlreadyExistsException : InvalidOperationException
{
    public ClientAlreadyExistsException(string email)
        : base($"A client with email '{email}' already exists.") { }
}

public sealed class ClientBlockedException : InvalidOperationException
{
    public ClientBlockedException(Guid id)
        : base($"Client '{id}' is blocked and cannot perform this operation.") { }
}

public class CategoryNotFoundException : KeyNotFoundException
{
    public CategoryNotFoundException(Guid id)
        : base($"Category with id '{id}' was not found.") { }

    public CategoryNotFoundException(string code)
        : base($"Category with code '{code}' was not found.") { }
}

public class CategoryAlreadyExistsException : InvalidOperationException
{
    public CategoryAlreadyExistsException(string code)
        : base($"A category with code '{code}' already exists.") { }
}

public class CategoryAssignedToUsersException : InvalidOperationException
{
    public CategoryAssignedToUsersException()
        : base($"This catgeory is assigned to existing clients.") { }

    public CategoryAssignedToUsersException(string message)
        : base(message) { }
}

public class DuplicateKeyException(string key) : Exception(key);