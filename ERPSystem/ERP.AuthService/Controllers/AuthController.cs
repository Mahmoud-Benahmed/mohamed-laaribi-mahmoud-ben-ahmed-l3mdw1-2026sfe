using ERP.AuthService.Application.DTOs;
using ERP.AuthService.Application.DTOs.AuthUser;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Properties;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP.AuthService.Controllers
{
    [ApiController]
    [Route(ApiRoutes.Auth.Base)]
    public class AuthController : ControllerBase
    {
        private readonly IAuthUserService _authService;

        public AuthController(IAuthUserService authService)
        {
            _authService = authService;
        }


        [HttpGet("me")]
        [ProducesResponseType(typeof(AuthUserGetResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMe()
        {
            // Extract authUserId from the JWT claim
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            if(!TryGetTenantId(out Guid tenantId))
                return BadRequest(new { error = "Tenant context required" });


            AuthUserGetResponseDto user = await _authService.GetByIdAsync(requesterId);

            if (user == null)
                return NotFound();

            if (tenantId != user.TenantId)
                return Forbid();

            return Ok(user);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AuthUserGetResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(Guid id)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            AuthUserGetResponseDto? user = await _authService.GetByIdAsync(id);
            if (user is null) 
                return NotFound();

            bool isSelf = requesterId == id;
            if (isSelf && !user.IsActive)
                return Forbid();

            bool hasAccess = User.HasClaim("privilege", Privileges.Users.VIEW_USERS);

            if (!isSelf && !hasAccess)
                return Forbid();

            return Ok(user);
        }


        [HttpGet("login/{login}")]
        [ProducesResponseType(typeof(AuthUserGetResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByLogin(string login)
        {
            AuthUserGetResponseDto result = await _authService.GetByLoginAsync(login);
            return Ok(result);
        }


        [HttpGet]
        [ProducesResponseType(typeof(PagedResultDto<AuthUserGetResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {

            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            PagedResultDto<AuthUserGetResponseDto> result = await _authService.GetAllAsync(pageNumber, pageSize, requesterId);
            return Ok(result);
        }

        [HttpGet("deactivated")]
        [ProducesResponseType(typeof(PagedResultDto<AuthUserGetResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDeactivated([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            PagedResultDto<AuthUserGetResponseDto> result = await _authService.GetPagedByStatusAsync(false, pageNumber, pageSize, requesterId);
            return Ok(result);
        }

        [HttpGet("activated")]
        [ProducesResponseType(typeof(PagedResultDto<AuthUserGetResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActivated([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            PagedResultDto<AuthUserGetResponseDto> result = await _authService.GetPagedByStatusAsync(true, pageNumber, pageSize, requesterId);
            return Ok(result);
        }

        [HttpPatch("{id:guid}/activate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Activate(Guid id)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            await _authService.ActivateAsync(id, requesterId);
            return NoContent();
        }


        [HttpPatch("{id:guid}/deactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            await _authService.DeactivateAsync(id, requesterId);
            return NoContent();
        }


        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteSoft(Guid id)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            await _authService.SoftDeleteAsync(id, requesterId);
            return NoContent();
        }

        [HttpPatch("restore/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Restore(Guid id)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            await _authService.RestoreAsync(id, requesterId);
            return NoContent();
        }

        [HttpGet("deleted")]
        [ProducesResponseType(typeof(PagedResultDto<AuthUserGetResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDeleted([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            PagedResultDto<AuthUserGetResponseDto> result = await _authService.GetDeletedPagedAsync(pageNumber, pageSize, requesterId);
            return Ok(result);
        }


        [HttpGet("by-role")]
        [ProducesResponseType(typeof(PagedResultDto<AuthUserGetResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByRole(
            [FromQuery] Guid roleId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            PagedResultDto<AuthUserGetResponseDto> result = await _authService.GetPagedByRoleAsync(roleId, pageNumber, pageSize, requesterId);
            return Ok(result);
        }


        [HttpGet("exists-login/{login}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExistsByLogin(string login)
        {
            bool result = await _authService.ExistsByLogin(login);
            return Ok(result);
        }

        [HttpGet("exists-email/{email}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExistsByEmail(string email)
        {
            bool result = await _authService.ExistsByEmail(email);
            return Ok(result);
        }

        [HttpGet("stats")]
        [ProducesResponseType(typeof(UserStatsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStats()
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            UserStatsDto result = await _authService.GetStatsAsync(requesterId);
            return Ok(result);
        }


        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthUserGetResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            if (!TryGetTenantId(out Guid tenantId))
                return BadRequest(new { error = "Tenant context required" });

            AuthUserGetResponseDto result = await _authService.RegisterAsync(request, requesterId, tenantId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("update/{id:guid}")]
        [ProducesResponseType(typeof(AuthUserGetResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateProfileDto updatedProfile)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();
            bool canManageUsers = User.HasClaim("privilege", Privileges.Users.UPDATE_USER);
            bool isOwner = requesterId == id;

            if (!isOwner && !canManageUsers)
                return Unauthorized();

            AuthUserGetResponseDto result = await _authService.UpdateProfile(id, updatedProfile);
            return Ok(result);
        }

        [HttpPut("update/{id:guid}/settings")]
        [ProducesResponseType(typeof(AuthUserGetResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateSettings([FromRoute] Guid id,
                                                        [FromBody] UserSettingsRequestDto settings)
        {
            if (!TryGetRequesterId(out Guid requesterId) || !requesterId.Equals(id))
                return Unauthorized();
            UserSettingsResponseDto result = await _authService.UpdateSettings(id, settings);
            return Ok(result);
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            AuthResponseDto result = await _authService.LoginAsync(request);
            return Ok(result);
        }


        [HttpPut("change-password/profile")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            await _authService.ChangePasswordAsync(
                    requesterId,
                    request);

            return NoContent();
        }


        [HttpPut("change-password/{userId:guid}")]
        public async Task<IActionResult> AdminChangePassword(Guid userId, [FromBody] AdminChangeProfileRequest request)
        {
            if (!TryGetRequesterId(out Guid requesterId))
                return Unauthorized();

            await _authService.ChangePasswordByAdminAsync(userId, request, requesterId);
            return NoContent();
        }


        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequestDto request)
        {

            AuthResponseDto result = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(result);
        }


        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke(RefreshTokenRequestDto request)
        {
            await _authService.RevokeRefreshTokenAsync(request.RefreshToken);
            return NoContent();
        }

        [HttpGet("validate-token")]
        public async Task<IActionResult> ValidateToken([FromHeader(Name = "Authorization")] string authorization)
        {
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
                return BadRequest(new { isValid = false, reason = "No token provided" });

            string token = authorization.Substring("Bearer ".Length);
            TokenValidationResultDto result = await _authService.ValidateTokenAsync(token);

            if (!result.IsValid)
                return Unauthorized(new { isValid = false, reason = result.ExpirationReason });

            return Ok(new
            {
                isValid = true,
                user = new
                {
                    result.UserId,
                    result.Login,
                    result.Email,
                    result.FullName,
                    result.IsActive
                }
            });
        }

        private bool TryGetRequesterId(out Guid requesterId)
        {
            requesterId = Guid.Empty;
            string? raw = HttpContext.Request.Headers["X-User-Id"].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out requesterId);
        }

        private bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = Guid.Empty;
            string? raw = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out tenantId);
        }
    }
}
