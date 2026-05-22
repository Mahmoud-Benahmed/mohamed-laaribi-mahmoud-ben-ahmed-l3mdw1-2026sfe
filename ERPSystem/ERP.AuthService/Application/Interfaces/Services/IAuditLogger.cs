using ERP.AuthService.Domain.Logger;

namespace ERP.AuthService.Application.Interfaces.Services
{
    public interface IAuditLogger
    {
        Task LogAsync(
            AuditAction action,
            bool success,
            Guid? tenantId = null,
            Guid? performedBy = null,
            Guid? targetUserId = null,
            string? failureReason = null,
            string? ipAddress = null,
            string? userAgent = null,
            Dictionary<string, string>? metadata = null);
    }
}