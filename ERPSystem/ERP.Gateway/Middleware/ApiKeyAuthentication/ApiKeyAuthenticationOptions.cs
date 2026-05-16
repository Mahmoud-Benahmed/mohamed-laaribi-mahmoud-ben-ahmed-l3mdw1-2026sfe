using Microsoft.AspNetCore.Authentication;

namespace ERP.Gateway.Middleware.ApiKeyAuthentication;
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
}