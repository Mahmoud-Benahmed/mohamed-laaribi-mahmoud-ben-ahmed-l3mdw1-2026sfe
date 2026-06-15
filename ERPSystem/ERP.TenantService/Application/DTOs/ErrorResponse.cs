namespace ERP.TenantService.Application.DTOs;

public class ErrorResponse
{
    public required string Code { get; set; }
    public required string Message { get; set; }
    public int StatusCode { get; set; }
}