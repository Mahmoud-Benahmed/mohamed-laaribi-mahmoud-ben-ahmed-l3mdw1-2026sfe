namespace ERP.PaymentService.Application.Exceptions;

public class PaymentNotFoundException : Exception
{
    public PaymentNotFoundException(Guid id)
        : base($"Payment with Id '{id}' was not found.") { }

    public PaymentNotFoundException(string number)
        : base($"Payment with number '{number}' was not found.") { }
}

public class PaymentAlreadyCancelledException : Exception
{
    public PaymentAlreadyCancelledException(Guid id)
        : base($"Payment '{id}' is already cancelled.") { }
}

public class PaymentDomainException : Exception
{
    public PaymentDomainException(string message)
        : base(message) { }
}

public class InvoiceNotFoundException : Exception
{
    public InvoiceNotFoundException(Guid id)
        : base($"Invoice '{id}' was not found in cache.") { }
}

public class InvoiceAlreadyCancelledException : Exception
{
    public InvoiceAlreadyCancelledException(Guid id)
        : base($"Invoice '{id}' is cancelled. Cannot allocate payment.") { }
}

public class InvoiceAlreadyPaidException : Exception
{
    public InvoiceAlreadyPaidException(Guid id)
        : base($"Invoice '{id}' is already fully paid.") { }
}

public class RefundExistsException : Exception
{
    public RefundExistsException(Guid invoiceId)
        : base($"A refund request for invoice '{invoiceId}' already exists.") { }
}