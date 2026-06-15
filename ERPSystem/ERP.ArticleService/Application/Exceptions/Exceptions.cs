// ── Category Exceptions

namespace ERP.ArticleService.Application.Exceptions;

public class CategoryNotFoundException : Exception
{
    public CategoryNotFoundException(Guid id)
        : base($"Category with id '{id}' was not found.") { }

    public CategoryNotFoundException(string name)
        : base($"Category with name '{name}' was not found.") { }
}
public class SubscriptionPlanNotFoundException(Guid id) : Exception($"SubscriptionPlan with id '{id}' was not found.");

public class CategoryAlreadyExistsException : Exception
{
    public CategoryAlreadyExistsException(string name)
        : base($"A category with the name '{name}' already exists.") { }
}

public class CategoryAssignedToArticlesException : InvalidOperationException
{
    public CategoryAssignedToArticlesException()
        : base($"This catgeory is assigned to existing clients.") { }

    public CategoryAssignedToArticlesException(string message)
        : base(message) { }
}


public class ArticleNotFoundException : Exception
{
    public ArticleNotFoundException(Guid id)
        : base($"Article with id '{id}' was not found.") { }

    public ArticleNotFoundException(string code)
        : base($"Article with code '{code}' was not found.") { }
}

public class ArticleAlreadyExistsException : Exception
{
    public ArticleAlreadyExistsException(string code)
        : base($"An article with code '{code}' already exists.") { }
}

public class ArticleAlreadyActiveException : Exception
{
    public ArticleAlreadyActiveException(Guid id)
        : base($"Article with id '{id}' is already restored.") { }
}

public class ArticleAlreadyInactiveException : Exception
{
    public ArticleAlreadyInactiveException(Guid id)
        : base($"Article with id '{id}' is already inactive.") { }
}

public class DuplicateKeyException(string key) : Exception(key);