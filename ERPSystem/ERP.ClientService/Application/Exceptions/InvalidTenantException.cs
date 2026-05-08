namespace ERP.ClientService.Application.Exceptions;

public class InvalidTenantException : Exception
{
    public InvalidTenantException() : base("Tenant slug is missing or invalid.") { }
}