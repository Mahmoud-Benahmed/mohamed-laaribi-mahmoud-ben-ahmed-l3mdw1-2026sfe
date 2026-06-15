namespace ERP.FournisseurService.Application.Exceptions;

public class FournisseurNotFoundException(Guid id)
    : KeyNotFoundException($"Fournisseur '{id}' was not found.");

public class FournisseurBlockedException(Guid id)
    : InvalidOperationException($"Fournisseur '{id}' is blocked.");
public class DuplicateKeyException(string key) : Exception(key);