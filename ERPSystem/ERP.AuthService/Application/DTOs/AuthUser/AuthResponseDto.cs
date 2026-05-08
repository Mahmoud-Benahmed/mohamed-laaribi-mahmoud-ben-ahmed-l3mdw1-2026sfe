namespace ERP.AuthService.Application.DTOs.AuthUser
{
    public record AuthResponseDto(
        string AccessToken,
        string RefreshToken,
        bool MustChangePassword,
        DateTime ExpiresAt,
        string? TenantSlug
    );
}
