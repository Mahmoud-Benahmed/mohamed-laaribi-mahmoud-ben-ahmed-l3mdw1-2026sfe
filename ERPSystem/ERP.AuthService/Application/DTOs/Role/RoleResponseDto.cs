namespace ERP.AuthService.Application.DTOs.Role
{
    public record RoleResponseDto(
       Guid Id,
       string Libelle,
       Guid? TenantId
    );

    public record RoleCreateDto(
       string Libelle
    );

    public record RoleUpdateDto(
       string Libelle
    );

}
