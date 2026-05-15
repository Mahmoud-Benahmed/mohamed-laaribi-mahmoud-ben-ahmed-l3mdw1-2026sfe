using ERP.AuthService.Application.DTOs;
using ERP.AuthService.Application.DTOs.AuthUser;

namespace ERP.AuthService.Application.Interfaces.Services;

public interface IAuthUserService
{
    Task<AuthUserGetResponseDto> RegisterAsync(RegisterRequestDto request, Guid performedById, Guid? tenantId= null);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequestDto request);
    Task ChangePasswordByAdminAsync(Guid userId, AdminChangeProfileRequest request, Guid adminId);

    Task ActivateAsync(Guid authUserId, Guid performedById);
    Task DeactivateAsync(Guid authUserId, Guid performedById);


    Task SoftDeleteAsync(Guid userId, Guid performedById);
    Task RestoreAsync(Guid userId, Guid performedById);
    Task<PagedResultDto<AuthUserGetResponseDto>> GetDeletedPagedAsync(int pageNumber, int pageSize, Guid? excludeId);

    Task<bool> ExistsByLogin(string login);
    Task<bool> ExistsByEmail(string email);

    Task<AuthUserGetResponseDto> UpdateProfile(Guid id, UpdateProfileDto request);
    Task<UserSettingsResponseDto> UpdateSettings(Guid userId, UserSettingsRequestDto dto);

    Task<AuthUserGetResponseDto> GetByIdAsync(Guid id);
    Task<AuthUserGetResponseDto> GetByLoginAsync(string login);
    Task<PagedResultDto<AuthUserGetResponseDto>> GetAllAsync(int pageN, int pageSize, Guid? excludeId); // <<<<<<<<<<<<<<<<<<<<<<<<<<<
    Task<PagedResultDto<AuthUserGetResponseDto>> GetPagedByStatusAsync(bool isActive, int pageNumber, int pageSize, Guid? excludeId); // <<<<<<<<<<<<<<<<<<<<<<<<<<<
    Task<PagedResultDto<AuthUserGetResponseDto>> GetPagedByRoleAsync(Guid roleId, int pageNumber, int pageSize, Guid? excludeId); // <<<<<<<<<<<<<<<<<<<<<<<<<<<


    Task<UserStatsDto> GetStatsAsync(Guid? excludeId = default); // <<<<<<<<<<<<<<<<<<<<<<<<<<<

    Task<RefreshTokenValidationResultDto> ValidateRefreshTokenAsync(string refreshToken);
    Task<TokenValidationResultDto> ValidateTokenAsync(string token);




}
