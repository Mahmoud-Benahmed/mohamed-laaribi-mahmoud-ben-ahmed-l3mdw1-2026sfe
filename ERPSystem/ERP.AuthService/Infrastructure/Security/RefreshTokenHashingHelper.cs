using System.Security.Cryptography;
using System.Text;

namespace ERP.AuthService.Infrastructure.Security;

public interface IRefreshTokenHashingHelper
{
    string GenerateRawToken();
    string Hash(string rawToken);
}
public class RefreshTokenHashingHelper : IRefreshTokenHashingHelper
{
    public string GenerateRawToken()
    {
        byte[] bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string Hash(string rawToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}