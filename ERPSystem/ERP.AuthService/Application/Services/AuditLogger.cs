using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain.Logger;

namespace ERP.AuthService.Application.Services
{
    public class AuditLogger : IAuditLogger
    {
        private readonly IAuditLogRepository _repository;
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(IAuditLogRepository repository, ILogger<AuditLogger> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task LogAsync(
            AuditAction action,
            bool success,
            Guid? tenantId = null,
            Guid? performedBy = null,
            Guid? targetUserId = null,
            string? failureReason = null,
            string? ipAddress = null,
            string? userAgent = null,
            Dictionary<string, string>? metadata = null)
        {
            AuditLog log = new AuditLog(
                action,
                success,
                tenantId,
                performedBy,
                targetUserId,
                failureReason,
                ipAddress,
                userAgent,
                metadata);

            try
            {
                await _repository.AddAsync(log);
            }
            catch (Exception ex)
            {
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {// Never let audit logging failure break the main flow
                    _logger.LogError(ex, "Failed to persist audit log for action {Action}", action);
                }
            }

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                // Also write to structured console log for observability
                if (success)
                    _logger.LogInformation("[AUDIT] {Action} | By: {PerformedBy} | Target: {TargetUserId} | IP: {IpAddress}",
                        action, performedBy, targetUserId, ipAddress);
                else
                    _logger.LogWarning("[AUDIT] {Action} FAILED | By: {PerformedBy} | Target: {TargetUserId} | Reason: {FailureReason} | IP: {IpAddress}",
                        action, performedBy, targetUserId, failureReason, ipAddress);
            }
        }
    }
}