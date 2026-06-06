namespace InvoiceService.Application.Exceptions
{
    public class InvoiceNotFoundException : Exception
    {
        public InvoiceNotFoundException(Guid id)
            : base($"Invoice with id '{id}' was not found.") { }
    }

    public class InvoiceAlreadyExistsException : Exception
    {
        public InvoiceAlreadyExistsException(string invoiceNumber)
            : base($"An invoice with number '{invoiceNumber}' already exists.") { }
    }

    public class InvoiceInvalidOperationException : Exception
    {
        public InvoiceInvalidOperationException(string message)
            : base(message) { }
    }
}

public class InvoiceDomainException : Exception
{
    public InvoiceDomainException(string message)
        : base(message) { }
}

public class ClientBlockedException : Exception 
{
    public ClientBlockedException(): base("Cannot create an invoice for a blocked client.") { }
}