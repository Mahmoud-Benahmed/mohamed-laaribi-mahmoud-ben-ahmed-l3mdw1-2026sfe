
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Gateway.Middleware.ApiKeyAuthentication;

public class ApiKeyValidator : IApiKeyValidator
{
    private readonly IConfiguration _configuration;

    public ApiKeyValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<ClaimsPrincipal?> ValidateAsync(string apiKey)
    {
        var validKey = _configuration["ApiKey:Secret"];

        if (string.IsNullOrWhiteSpace(validKey))
            return Task.FromResult<ClaimsPrincipal?>(null);

        var a = Encoding.UTF8.GetBytes(apiKey);
        var b = Encoding.UTF8.GetBytes(validKey);

        // Pad to same length before comparing to avoid length-based timing leak
        var maxLen = Math.Max(a.Length, b.Length);
        var aPadded = new byte[maxLen];
        var bPadded = new byte[maxLen];
        a.CopyTo(aPadded, 0);
        b.CopyTo(bPadded, 0);

        if (!CryptographicOperations.FixedTimeEquals(aPadded, bPadded))
            return Task.FromResult<ClaimsPrincipal?>(null);

        var claims = new[] { new Claim(ClaimTypes.Name, "ApiKeyClient") };
        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.DefaultScheme);
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult<ClaimsPrincipal?>(principal);
    }
}