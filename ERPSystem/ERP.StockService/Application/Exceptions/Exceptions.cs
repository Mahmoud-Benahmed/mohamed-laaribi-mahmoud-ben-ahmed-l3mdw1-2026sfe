namespace ERP.StockService.Application.Exceptions;

public class BonNotFoundException(Guid id)
    : KeyNotFoundException($"Bon with '{id}' was not found.");
public class BonEntreNotFoundException(Guid id)
    : KeyNotFoundException($"BonEntre '{id}' was not found.");

public class BonSortieNotFoundException(Guid id)
    : KeyNotFoundException($"BonSortie '{id}' was not found.");

public class BonRetourNotFoundException(Guid id)
    : KeyNotFoundException($"BonRetour '{id}' was not found.");

public class ArticleNotInSourceBonException : Exception
{
    public ArticleNotInSourceBonException(Guid articleId, Guid sourceId)
        : base($"Article '{articleId}' was not found in source bon '{sourceId}'.")
    {
    }
}
public class RetourQuantityExceedsSourceException : Exception
{
    public RetourQuantityExceedsSourceException(Guid articleId, decimal requested, decimal max)
        : base($"Returned quantity {(int)requested} for article '{articleId}' exceeds source quantity of {(int)max}.")
    {
    }
}

public class FournisseurNotFoundException(Guid id)
    : KeyNotFoundException($"Fournisseur '{id}' was not found.");

public class ClientNotFoundException : Exception
{
    public ClientNotFoundException(Guid id)
        : base($"Client with id '{id}' was not found.") { }
}

public sealed class ClientBlockedException : InvalidOperationException
{
    public ClientBlockedException(Guid id)
        : base($"Client '{id}' is blocked and cannot perform this operation.") { }
}


public class FournisseurBlockedException(Guid id)
    : InvalidOperationException($"Fournisseur '{id}' is blocked.");

public class ArticleNotFoundException : Exception
{
    public ArticleNotFoundException(Guid id)
        : base($"Article with id '{id}' was not found.") { }

    public ArticleNotFoundException(string code)
        : base($"Article with code '{code}' was not found.") { }
}

public class InsufficientStockException(Guid articleId, decimal available, decimal requested)
    : Exception($"Insufficient stock for article {articleId}: available={available}, requested={requested}");