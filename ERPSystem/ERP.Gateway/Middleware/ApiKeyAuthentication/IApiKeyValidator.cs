using System.Security.Claims;

namespace ERP.Gateway.Middleware.ApiKeyAuthentication;

public interface IApiKeyValidator
{
    Task<ClaimsPrincipal?> ValidateAsync(string apiKey);
}