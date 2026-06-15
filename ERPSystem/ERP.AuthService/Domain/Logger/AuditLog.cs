using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ERP.AuthService.Domain.Logger
{
    public class AuditLog
    {

        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public AuditAction Action { get; set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? TenantId { get; private set; }
        public bool IsGlobal => TenantId == null;

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? PerformedBy { get; set; }   // null for system actions (e.g. token refresh)

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? TargetUserId { get; set; }   // null if action is not user-targeted

        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public DateTime Timestamp { get; set; }

        public AuditLog(
            AuditAction action,
            bool success,
            Guid? tenantId= null,
            Guid? performedBy = null,
            Guid? targetUserId = null,
            string? failureReason = null,
            string? ipAddress = null,
            string? userAgent = null,
            Dictionary<string, string>? metadata = null)
        {
            Id = Guid.NewGuid();
            Action = action;
            Success = success;
            TenantId = tenantId;
            PerformedBy = performedBy;
            TargetUserId = targetUserId;
            FailureReason = failureReason;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            Metadata = metadata;
            Timestamp = DateTime.UtcNow;
        }
    }

}