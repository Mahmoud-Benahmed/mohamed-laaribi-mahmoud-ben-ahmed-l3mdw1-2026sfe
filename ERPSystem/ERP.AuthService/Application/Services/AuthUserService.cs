using ERP.AuthService.Application.DTOs;
using ERP.AuthService.Application.DTOs.AuthUser;
using ERP.AuthService.Application.Exceptions;
using ERP.AuthService.Application.Interfaces;
using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain;
using ERP.AuthService.Domain.Cache;
using ERP.AuthService.Domain.Logger;
using ERP.AuthService.Infrastructure.ApiClient;
using ERP.AuthService.Infrastructure.Security;
using ERP.AuthService.Properties;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;


namespace ERP.AuthService.Application.Services
{
    public class AuthUserService : IAuthUserService
    {
        private readonly IAuditLogger _auditLogger;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IAuthUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IJwtTokenGenerator _jwtGenerator;
        private readonly IPasswordHasher<AuthUser> _passwordHasher;
        private readonly ILogger<AuthUserService> _logger;
        private readonly IControleRepository _controleRepository;
        private readonly IPrivilegeRepository _privilegeRepository;
        private readonly IRefreshTokenHashingHelper _refreshTokenHelper;
        private readonly ITenantApiClient _tenantApi;
        private readonly ITenantCacheRepository _tenantRepo;

        private readonly ITenantContext _tenantContext;


        public AuthUserService(
            IAuditLogger auditLogger,
            IHttpContextAccessor httpContextAccessor,
            IAuthUserRepository userRepository,
            IRoleRepository roleRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IJwtTokenGenerator jwtGenerator,
            IPasswordHasher<AuthUser> passwordHasher,
            IControleRepository controleRepository,
            IPrivilegeRepository privilegeRepository,
            ITenantContext tenantContext,
            IRefreshTokenHashingHelper refreshTokenHelper,
            ITenantApiClient tenantApi,
            ITenantCacheRepository tenantRepo,
            ILogger<AuthUserService> logger
            )
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtGenerator = jwtGenerator;
            _passwordHasher = passwordHasher;
            _controleRepository = controleRepository;
            _privilegeRepository = privilegeRepository;
            _auditLogger = auditLogger;
            _httpContext = httpContextAccessor;
            _tenantContext = tenantContext;
            _refreshTokenHelper = refreshTokenHelper;
            _tenantApi = tenantApi;
            _tenantRepo = tenantRepo;
            _logger = logger;
        }

        // ============================================
        // READ
        // ============================================

        public async Task<PagedResultDto<AuthUserGetResponseDto>> GetAllAsync(int pageNumber, int pageSize, Guid? excludeId)
        {
            ValidatePaging(pageNumber, pageSize);

            (List<AuthUser>? items, int totalCount) = await _userRepository.GetAllAsync(pageNumber, pageSize, excludeId);

            AuthUserGetResponseDto[] mapped = await Task.WhenAll(items.Select(MapToDtoAsync));

            return new PagedResultDto<AuthUserGetResponseDto>(
                mapped,
                totalCount,
                pageNumber,
                pageSize);
        }

        public async Task<PagedResultDto<AuthUserGetResponseDto>> GetPagedByStatusAsync(bool isActive, int pageNumber, int pageSize, Guid? excludeId)
        {

            ValidatePaging(pageNumber, pageSize);

            (List<AuthUser>? items, int totalCount) = await _userRepository.GetPagedByStatusAsync(isActive, pageNumber, pageSize, excludeId);

            AuthUserGetResponseDto[] mapped = await Task.WhenAll(items.Select(MapToDtoAsync));

            return new PagedResultDto<AuthUserGetResponseDto>(
                mapped,
                totalCount,
                pageNumber,
                pageSize);
        }

        public async Task<PagedResultDto<AuthUserGetResponseDto>> GetPagedByRoleAsync(Guid roleId, int pageNumber, int pageSize, Guid? excludeId)
        {

            ValidatePaging(pageNumber, pageSize);

            (List<AuthUser>? items, int totalCount) = await _userRepository.GetPagedByRoleAsync(roleId, pageNumber, pageSize, excludeId);

            AuthUserGetResponseDto[] mapped = await Task.WhenAll(items.Select(MapToDtoAsync));

            return new PagedResultDto<AuthUserGetResponseDto>(
                mapped,
                totalCount,
                pageNumber,
                pageSize);
        }

        public async Task<AuthUserGetResponseDto> GetByIdAsync(Guid id)
        {
            AuthUser user = await _userRepository.GetByIdAsync(id)
                      ?? throw new UserNotFoundException(id);

            return await MapToDtoAsync(user);
        }

        public async Task<AuthUserGetResponseDto> GetByLoginAsync(string login)
        {
            AuthUser user = await _userRepository.GetByLoginAsync(login)
                        ?? throw new UserNotFoundException(login);

            return await MapToDtoAsync(user);
        }

        public async Task<bool> ExistsByEmail(string email)
        {
            return await _userRepository.ExistsByEmailAsync(email);
        }

        public async Task<bool> ExistsByLogin(string login)
        {
            return await _userRepository.ExistsByLoginAsync(login);
        }




        // ===============================
        // PROFILE UPDATE, CREATE, LOGIN
        // ===============================
        public async Task<AuthUserGetResponseDto> UpdateProfile(Guid id, UpdateProfileDto request)
        {
            AuthUser user = await _userRepository.GetByIdAsync(id) ?? throw new UserNotFoundException(id);
            user.UpdateProfile(request.FullName, request.Email);
            await _userRepository.UpdateAsync(user);

            await _auditLogger.LogAsync(
                AuditAction.ProfileUpdated,
                success: true,
                performedBy: id,
                targetUserId: id,
                metadata: new() { ["email"] = user.Email, ["fullName"] = user.FullName },
                ipAddress: GetIp());

            return await MapToDtoAsync(user);
        }

        public async Task<UserSettingsResponseDto> UpdateSettings(Guid userId, UserSettingsRequestDto dto)
        {
            AuthUser user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UserNotFoundException(userId);

            user.UpdateSettings(dto.Theme.ToString(), dto.Language.ToString());
            await _userRepository.UpdateAsync(user);

            return new UserSettingsResponseDto(
                Theme: user.Settings.Theme.ToString(),
                Language: user.Settings.Language.ToString()
            );
        }

        public async Task<AuthUserGetResponseDto> RegisterAsync(RegisterRequestDto request, Guid performedById, Guid? tenantId)
        {
            if (await _userRepository.ExistsByLoginAsync(request.Login))
                throw new LoginAlreadyExsistException();

            if (await _userRepository.ExistsByEmailAsync(request.Email))
                throw new EmailAlreadyExistsException();

            if (_tenantContext.TenantId is not null)
            {
                _logger.LogInformation(
                    "Validating subscription limits for tenant '{TenantId}'",
                    _tenantContext.TenantId);

                var subscription =
                    await _tenantApi.ResolveAsync(_tenantContext.TenantId.Value);

                if (subscription?.Plan is null)
                {
                    _logger.LogWarning(
                        "Tenant '{TenantId}' has no active subscription plan",
                        _tenantContext.TenantId);

                    return null;
                }

                _logger.LogInformation(
                    "Tenant '{TenantId}' uses plan '{PlanCode}' with max users {MaxUsers}",
                    subscription.TenantId,
                    subscription.Plan.Code,
                    subscription.Plan.MaxUsers);

                int currentUserCount = await _userRepository.CountByTenantIdAsync(_tenantContext.TenantId.Value);

                _logger.LogInformation(
                    "Tenant '{TenantId}' current user count: {CurrentUserCount}",
                    subscription.TenantId,
                    currentUserCount);

                if (currentUserCount >= subscription.Plan.MaxUsers)
                {
                    _logger.LogWarning(
                        "Tenant '{TenantId}' exceeded user limit ({CurrentUserCount}/{MaxUsers})",
                        subscription.TenantId,
                        currentUserCount,
                        subscription.Plan.MaxUsers);
                    throw new TenantUserLimitReachedException();
                }

                _logger.LogInformation(
                    "Tenant '{TenantId}' passed subscription validation",
                    subscription.TenantId);
            }

            Role role = await _roleRepository.GetByIdAsync(request.RoleId) ?? throw new InvalidOperationException("Role not found.");
            if (role.IsGlobal && tenantId.HasValue)
                throw new InvalidOperationException($"Cannot assign a global role to a tenant user. {tenantId}");

            string capitalizedFullName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(request.FullName.ToLower());

            AuthUser user = new AuthUser(request.Login, request.Email, capitalizedFullName, role.Id, tenantId: _tenantContext.TenantId);


            string hashedPassword = _passwordHasher.HashPassword(user, request.Password);
            user.SetPasswordHash(hashedPassword);

            await _userRepository.AddAsync(user);

            await _auditLogger.LogAsync(
                AuditAction.UserRegistered,
                success: true,
                performedBy: performedById,
                targetUserId: user.Id,
                metadata: new() { ["login"] = request.Login, ["email"] = request.Email },
                ipAddress: GetIp());

            return await MapToDtoAsync(user);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            try
            {
                AuthUser user = await _userRepository.GetByLoginAsync(request.Login)
                    ?? throw new InvalidCredentialsException();

                if (!user.CanLogin())
                    throw new UserInactiveException("Sorry, you cannot login because your account is disabled.");


                PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    request.Password
                );

                if (result == PasswordVerificationResult.Failed)
                    throw new InvalidCredentialsException();

                user.RecordLogin();
                await _userRepository.UpdateAsync(user);

                AuthResponseDto token = await GenerateAuthResponseAsync(user);
                await _auditLogger.LogAsync(
                        AuditAction.Login,
                        success: true,
                        performedBy: user.Id,
                        metadata: new() { ["login"] = request.Login },
                        ipAddress: GetIp(),
                        userAgent: GetUserAgent());
                return token;
            }
            catch (InvalidCredentialsException ex)
            {
                await _auditLogger.LogAsync(
                        AuditAction.Login,
                        success: false,
                        failureReason: ex.Message,
                        metadata: new() { ["login"] = request.Login },
                        ipAddress: GetIp(),
                        userAgent: GetUserAgent());
                throw;
            }
            catch (UserInactiveException)
            {
                await _auditLogger.LogAsync(
                    AuditAction.Login,
                    success: false,
                    failureReason: "Account inactive",
                    metadata: new() { ["login"] = request.Login },
                    ipAddress: GetIp(),
                    userAgent: GetUserAgent());
                throw;
            }
        }


        // ======================
        // TOKEN MANAGEMENT
        // ======================
        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            RefreshToken token = await _refreshTokenRepository.GetByTokenAsync(refreshToken)
                ?? throw new InvalidRefreshTokenException();

            if (token.IsExpired())
                throw new UnauthorizedAccessException("Refresh token expired.");

            if (token.IsRevoked)
            {
                throw new TokenAlreadyRevokedException();
            }

            AuthUser? user = await _userRepository.GetByIdAsync(token.UserId);

            if (user is null)
            {
                await _refreshTokenRepository.RevokeAllByUserIdAsync(token.UserId);
                throw new UnauthorizedAccessException("User associated with the refresh token NOT FOUND");
            }

            // Rotate token
            await RevokeRefreshTokenAsyncPrivate(token);// revoke the token to refresh before getting a fresh one

            AuthResponseDto result = await GenerateAuthResponseAsync(user);

            await _auditLogger.LogAsync(
                    AuditAction.TokenRefreshed,
                    success: true,
                    performedBy: user.Id,
                    ipAddress: GetIp());

            return result;
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            RefreshToken token = await _refreshTokenRepository.GetByTokenAsync(refreshToken) ?? throw new InvalidRefreshTokenException();
            await RevokeRefreshTokenAsyncPrivate(token);
            await _auditLogger.LogAsync(
                AuditAction.Logout,
                success: true,
                performedBy: token.UserId,
                ipAddress: GetIp());
        }


        private async Task RevokeRefreshTokenAsyncPrivate(RefreshToken token)
        {
            AuthUser? user = await _userRepository.GetByIdAsync(token.UserId);

            if (user is null)
            {
                await _refreshTokenRepository.RevokeAllByUserIdAsync(token.UserId);
                throw new UnauthorizedAccessException("User associated with the refresh token NOT FOUND");
            }

            if (token.IsRevoked)
                throw new TokenAlreadyRevokedException();

            token.Revoke();
            await _refreshTokenRepository.UpdateAsync(token);
        }

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(AuthUser user)
        {
            Role role = await _roleRepository.GetByIdAsync(user.RoleId)
                       ?? throw new InvalidOperationException("Role not found.");

            List<Privilege> privileges = await _privilegeRepository.GetByRoleIdAsync(user.RoleId);

            List<Guid> grantedControleIds = privileges
                .Where(p => p.IsGranted)
                .Select(p => p.ControleId)
                .ToList();

            List<Controle> controles = await _controleRepository.GetByIdsAsync(grantedControleIds);

            bool isTenantUser = user.TenantId.HasValue;
            List<string> privilegeNames = controles
                .Where(c => !isTenantUser || c.Category != "TENANT")
                .Select(c => c.Libelle)
                .ToList();

            TenantCache? tenant = null;

            _logger.LogWarning("User TenantId: {TenantId}", user.TenantId);

            if (user.TenantId.HasValue)
                tenant = await _tenantRepo.GetByIdAsync(user.TenantId.Value);

            _logger.LogWarning("Resolved tenant Id: {TenantCacheId}", tenant?.Id);

            (string? accessToken, DateTime expiresAt) = _jwtGenerator.GenerateAccessToken(
                user.Id,
                user.Login,
                role.Libelle,
                privilegeNames,
                user.TenantId,
                tenant?.Slug
            );

            string refreshTokenRaw = _refreshTokenHelper.GenerateRawToken();
            string tokenHashed= _refreshTokenHelper.Hash(refreshTokenRaw);

            RefreshToken refreshToken = new RefreshToken(
                user.Id,
                tokenHashed,
                DateTime.UtcNow.AddDays(7),
                tenantId: user.TenantId
            );

            await _refreshTokenRepository.AddAsync(refreshToken);

            return new AuthResponseDto(
                accessToken,
                refreshTokenRaw,
                user.MustChangePassword,
                expiresAt
            );
        }

        // ======================
        // CHANGE PASSWORD
        // ======================
        public async Task ChangePasswordAsync(Guid id, ChangePasswordRequestDto request)
        {
            AuthUser user = await _userRepository.GetByIdAsync(id)
                        ?? throw new UserNotFoundException(id);

            if (!user.IsActive)
                throw new UserInactiveException();

            if (request.CurrentPassword.Equals(request.NewPassword))
                throw new ArgumentException("The new password cannot be the same as the current password.");

            PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (result == PasswordVerificationResult.Failed)
                throw new InvalidCredentialsException();

            string newHashedPassword = _passwordHasher.HashPassword(user, request.NewPassword);
            user.ChangePassword(newHashedPassword);
            if (user.MustChangePassword) user.MustChangePassword = false;

            await _userRepository.UpdateAsync(user);

            await _auditLogger.LogAsync(
                  AuditAction.PasswordChanged,
                  success: true,
                  performedBy: id,
                  ipAddress: GetIp(),
                  metadata: new() { ["login"] = user.Login });
        }

        public async Task ChangePasswordByAdminAsync(Guid userId, AdminChangeProfileRequest request, Guid adminId)
        {
            AuthUser user = await _userRepository.GetByIdAsync(userId)
                       ?? throw new UserNotFoundException(userId);

            string hashedNewPassword = _passwordHasher.HashPassword(user, request.NewPassword);

            user.ChangePassword(hashedNewPassword);
            user.MustChangePassword = true;

            await _userRepository.UpdateAsync(user);
            await _auditLogger.LogAsync(
                    AuditAction.PasswordChangedByAdmin,
                        success: true,
                        performedBy: adminId,
                        targetUserId: userId,
                        ipAddress: GetIp(),
                        metadata: new() { ["login"] = user.Login });
        }


        // ======================
        // ACTIVATE/DEACTIVATE 
        // ======================
        public async Task ActivateAsync(Guid authUserId, Guid performedById)
        {
            if (authUserId == performedById)
                throw new UnauthorizedOperationException("You cannot apply this operation on your account.");

            AuthUser user = await _userRepository.GetByIdAsync(authUserId)
                       ?? throw new UserNotFoundException(authUserId);

            AuthUser performedBy = await _userRepository.GetByIdAsync(performedById)
                       ?? throw new UserNotFoundException(performedById);

            if (user.IsActive)
                throw new UserActiveException();

            user.Activate();
            await _userRepository.UpdateAsync(user);

            await _auditLogger.LogAsync(
                AuditAction.UserActivated,
                success: true,
                performedBy: performedById,
                targetUserId: user.Id,
                ipAddress: GetIp());
        }

        public async Task DeactivateAsync(Guid authUserId, Guid performedById)
        {
            if (authUserId == performedById)
                throw new UnauthorizedOperationException("You cannot apply this operation on your account.");

            AuthUser user = await _userRepository.GetByIdAsync(authUserId)
                       ?? throw new UserNotFoundException(authUserId);
            if (!user.IsActive)
                throw new UserInactiveException();

            AuthUser performedBy = await _userRepository.GetByIdAsync(performedById)
                       ?? throw new UserNotFoundException(performedById);

            user.Deactivate();
            await _userRepository.UpdateAsync(user);
            await _auditLogger.LogAsync(
                    AuditAction.UserDeactivated,
                    success: true,
                    performedBy: performedById,
                    targetUserId: user.Id,
                    ipAddress: GetIp());
        }

        // ======================
        // SOFT DELETE
        // ======================
        public async Task SoftDeleteAsync(Guid deletedId, Guid performedById)
        {
            // check if whoever is performing this operation exists and is active (we don't want to log a delete action performed by a deleted user for example)
            AuthUser performedBy = await _userRepository.GetByIdAsync(performedById) ?? throw new UserNotFoundException(performedById);
            
            if (deletedId == performedById)
                throw new UnauthorizedOperationException("You cannot apply this operation on your account.");

            AuthUser user = await _userRepository.GetByIdAsync(deletedId)
                        ?? throw new UserNotFoundException(deletedId);

            Role userRole = await _roleRepository.GetByIdAsync(user.RoleId) ?? throw new RoleNotFoundException(user.RoleId);

            if (userRole.Libelle.Equals(Roles.SystemAdmin))
            {
                int adminCount= await _userRepository.CountByRoleIdAsync(user.RoleId);
                if (adminCount <= 1)
                    throw new InvalidOperationException("Cannot delete the last system administrator.");
            }

            if (user.IsDeleted) {
                await _auditLogger.LogAsync(
                    AuditAction.UserDeleted,
                    success: true,
                    performedBy: performedById,
                    targetUserId: user.Id,
                    ipAddress: GetIp(),
                    metadata: new() { ["deleted"] = user.Login, ["deletedBy"] = performedById.ToString() });
                return;
            } 

            user.Delete();
            await _userRepository.UpdateAsync(user);

            await _auditLogger.LogAsync(
                    AuditAction.UserDeleted,
                    success: true,
                    performedBy: performedById,
                    targetUserId: user.Id,
                    ipAddress: GetIp(),
                    metadata: new() { ["deleted"] = user.Login, ["deletedBy"] = performedById.ToString() });
        }

        public async Task RestoreAsync(Guid deletedId, Guid performedById)
        {
            if (deletedId == performedById)
                throw new UnauthorizedOperationException("You cannot apply this operation on your account.");

            AuthUser performedBy = await _userRepository.GetByIdAsync(performedById)
                ?? throw new UserNotFoundException(performedById);

            if (performedBy.IsDeleted)
                throw new UnauthorizedOperationException("Your account is inactive.");

            AuthUser user = await _userRepository.GetByDeletedIdAsync(deletedId)
                ?? throw new UserNotFoundException(deletedId);

            if (!user.IsDeleted) return;

            if (_tenantContext.TenantId is not null)
            {
                var subscription = await _tenantApi.ResolveAsync(_tenantContext.TenantId.Value);

                if (subscription?.Plan is null)
                {
                    _logger.LogWarning(
                        "Tenant '{TenantId}' has no active subscription plan",
                        _tenantContext.TenantId);

                    throw new InvalidOperationException(
                        "Unable to restore user: No active subscription plan found.");
                }

                int currentUserCount = await _userRepository.CountByTenantIdAsync(_tenantContext.TenantId.Value);

                if (currentUserCount >= subscription.Plan.MaxUsers)
                {
                    _logger.LogWarning(
                        "Tenant '{TenantId}' exceeded user limit ({CurrentUserCount}/{MaxUsers})",
                        _tenantContext.TenantId,
                        currentUserCount,
                        subscription.Plan.MaxUsers);

                    throw new InvalidOperationException(
                        "Unable to restore user: Tenant user limit reached.");
                }
            }

            user.Restore();
            await _userRepository.UpdateAsync(user);

            await _auditLogger.LogAsync(
                AuditAction.UserRestored,
                success: true,
                performedBy: performedById,
                targetUserId: user.Id,
                ipAddress: GetIp(),
                metadata: new() { ["restored"] = user.Login, ["restoredBy"] = performedById.ToString() });
        } 

        public async Task<PagedResultDto<AuthUserGetResponseDto>> GetDeletedPagedAsync(int pageNumber, int pageSize, Guid? excludeId)
        {
            ValidatePaging(pageNumber, pageSize);
            (List<AuthUser>? items, int totalCount) = await _userRepository.GetDeletedPagedAsync(pageNumber, pageSize, excludeId);

            AuthUserGetResponseDto[] mapped = await Task.WhenAll(items.Select(MapToDtoAsync));

            return new PagedResultDto<AuthUserGetResponseDto>(
                mapped,
                totalCount,
                pageNumber,
                pageSize);
        }




        // ======================
        // STATS
        // ======================
        public async Task<UserStatsDto> GetStatsAsync(Guid? excludeId = default)
        {
            return await _userRepository.GetStatsAsync(excludeId);
        }

        // Add this method to your AuthUserService class

        /// <summary>
        /// Validates a JWT token and returns the associated user if valid and exists
        /// </summary>
        /// <param name="token">The JWT access token</param>
        /// <returns>User info if token is valid and user exists</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when token is invalid or user doesn't exist</exception>
        public async Task<TokenValidationResultDto> ValidateTokenAsync(string token)
        {
            try
            {
                ClaimsPrincipal? principal = _jwtGenerator.ValidateToken(token);

                if (principal == null)
                    throw new UnauthorizedAccessException("Invalid token signature or structure.");

                Claim? userIdClaim = principal.FindFirst("sub") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                    throw new UnauthorizedAccessException("Token does not contain a valid user identifier.");

                AuthUser? user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new UnauthorizedAccessException($"User with ID '{userId}' no longer exists.");

                if (!user.IsActive)
                    throw new UnauthorizedAccessException("User account is deactivated.");

                if (user.IsDeleted)
                    throw new UnauthorizedAccessException("User account has been deleted.");


                await _auditLogger.LogAsync(
                    AuditAction.TokenValidated,
                    success: true,
                    performedBy: userId,
                    ipAddress: GetIp(),
                    userAgent: GetUserAgent(),
                    metadata: new() { ["token_validation"] = "success" }
                );

                // 8. Return validation result
                return new TokenValidationResultDto(
                    IsValid: true,
                    UserId: user.Id,
                    Login: user.Login,
                    Email: user.Email,
                    FullName: user.FullName,
                    RoleId: user.RoleId,
                    IsActive: user.IsActive,
                    ExpirationReason: null
                );
            }
            catch (SecurityTokenExpiredException)
            {
                await LogTokenValidationFailure("Token expired", token);
                return new TokenValidationResultDto(
                    IsValid: false,
                    UserId: null,
                    Login: null,
                    Email: null,
                    FullName: null,
                    RoleId: null,
                    IsActive: false,
                    ExpirationReason: "Token has expired"
                );
            }
            catch (SecurityTokenValidationException ex)
            {
                await LogTokenValidationFailure($"Invalid token: {ex.Message}", token);
                throw new UnauthorizedAccessException($"Token validation failed: {ex.Message}");
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException)
            {
                await LogTokenValidationFailure($"Validation error: {ex.Message}", token);
                throw new UnauthorizedAccessException("Token validation failed due to internal error.");
            }
        }

        /// <summary>
        /// Validates a refresh token
        /// </summary>
        public async Task<RefreshTokenValidationResultDto> ValidateRefreshTokenAsync(string refreshToken)
        {
            try
            {
                // 1. Check if refresh token exists in database
                RefreshToken? token = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

                if (token == null)
                    return new RefreshTokenValidationResultDto(
                        IsValid: false,
                        UserId: null,
                        ExpirationReason: "Refresh token not found"
                    );

                // 2. Check if token is expired
                if (token.IsExpired())
                    return new RefreshTokenValidationResultDto(
                        IsValid: false,
                        UserId: token.UserId,
                        ExpirationReason: "Refresh token has expired"
                    );

                // 3. Check if token is revoked
                if (token.IsRevoked)
                    return new RefreshTokenValidationResultDto(
                        IsValid: false,
                        UserId: token.UserId,
                        ExpirationReason: "Refresh token has been revoked"
                    );

                // 4. Check if user still exists and is active
                AuthUser? user = await _userRepository.GetByIdAsync(token.UserId);
                if (user == null)
                    return new RefreshTokenValidationResultDto(
                        IsValid: false,
                        UserId: token.UserId,
                        ExpirationReason: "User associated with token no longer exists"
                    );

                if (!user.IsActive)
                    return new RefreshTokenValidationResultDto(
                        IsValid: false,
                        UserId: token.UserId,
                        ExpirationReason: "User account is deactivated"
                    );

                // 5. Return success
                return new RefreshTokenValidationResultDto(
                    IsValid: true,
                    UserId: token.UserId,
                    ExpirationReason: null
                );
            }
            catch (Exception ex)
            {
                await _auditLogger.LogAsync(
                    AuditAction.TokenValidationFailed,
                    success: false,
                    failureReason: ex.Message,
                    ipAddress: GetIp(),
                    metadata: new() { ["token_type"] = "refresh" }
                );

                return new RefreshTokenValidationResultDto(
                    IsValid: false,
                    UserId: null,
                    ExpirationReason: $"Validation error: {ex.Message}"
                );
            }
        }

        private async Task LogTokenValidationFailure(string reason, string token)
        {
            // Only log first few characters of token for security
            string? tokenPreview = token?.Length > 20 ? token.Substring(0, 20) + "..." : token;

            await _auditLogger.LogAsync(
                AuditAction.TokenValidationFailed,
                success: false,
                failureReason: reason,
                ipAddress: GetIp(),
                userAgent: GetUserAgent(),
                metadata: new()
                {
                    ["token_preview"] = tokenPreview,
                    ["validation_type"] = "access_token"
                }
            );
        }

        // ======================
        // DTO MAPPING HELPER
        // ======================

        private async Task<AuthUserGetResponseDto> MapToDtoAsync(AuthUser user)
        {
            Role role = await _roleRepository.GetByIdAsync(user.RoleId)
                       ?? throw new UnauthorizedAccessException("Role not found.");

            return new AuthUserGetResponseDto(
                Id: user.Id,
                Email: user.Email,
                FullName: user.FullName,
                Login: user.Login,
                RoleId: user.RoleId,
                RoleName: role.Libelle.ToString(),
                MustChangePassword: user.MustChangePassword,
                IsActive: user.IsActive,
                Settings: MapUserSettingsToDto(user.Settings),
                CreatedAt: user.CreatedAt,
                TenantId: user.TenantId,
                UpdatedAt: user.UpdatedAt,
                LastLoginAt: user.LastLoginAt
            );
        }

        private UserSettingsResponseDto MapUserSettingsToDto(UserSettings settings)
        {
            return new UserSettingsResponseDto(
                Theme: settings.Theme.ToString(),
                Language: settings.Language.ToString()
            );
        }


        private string? GetIp()
        {
            IPAddress? ip = _httpContext?.HttpContext?.Connection.RemoteIpAddress;

            if (ip == null)
                return null;

            // Convert IPv6-mapped IPv4 to normal IPv4
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                ip = ip.MapToIPv4();

            return ip.ToString();
        }

        private string? GetUserAgent()
            => _httpContext?.HttpContext?.Request.Headers["User-Agent"].ToString();


        private static void ValidatePaging(int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    "Page number must be greater than zero.");
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize),
                    "Page size must be greater than zero.");
        }
    }
}