namespace ERP.AuthService.Application.DTOs.Role
{
    public record PrivilegeResponseDto(
        Guid Id,
        Guid RoleId,
        Guid ControleId,
        string ControleLibelle,
        string ControleCategory,
        bool IsGranted,
        Guid? TenantId
    );
}
