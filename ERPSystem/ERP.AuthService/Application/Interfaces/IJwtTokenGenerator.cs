using ERP.AuthService.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ERP.AuthService.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        (string Token, DateTime ExpiresAt) GenerateAccessToken(
            Guid userId,
            string login,
            string role,
            IEnumerable<string> privileges,
            Guid? tenantId= null);

        // Basic validation - returns ClaimsPrincipal if valid, null if invalid
        ClaimsPrincipal? ValidateToken(string token);

        // Detailed validation - returns TokenValidationResult with error information
        CustomTokenValidationResult ValidateTokenWithDetails(string token);

        // Read token without validation (for debugging)
        JwtSecurityToken? ReadToken(string token);

        // Quick expiration check
        bool IsTokenExpired(string token);

        // Get remaining lifetime in seconds
        double GetTokenRemainingLifetime(string token);
    }
}