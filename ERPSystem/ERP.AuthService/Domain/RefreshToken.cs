using ERP.AuthService.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ERP.AuthService.Domain
{
    public class RefreshToken : ITenantFilterable
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid UserId { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? TenantId { get; private set; }
        public bool IsGlobal => TenantId == null;

        public string Token { get; set; }

        public DateTime ExpiresAt { get; private set; }

        public bool IsRevoked { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? RevokedAt { get; private set; }


        [BsonConstructor]
        private RefreshToken() { }

        public RefreshToken(Guid userId, string token, DateTime expiresAt, Guid? tenantId=null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Token = token;
            TenantId= tenantId;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            IsRevoked = false;
            RevokedAt = null;
        }

        public void Revoke()
        {
            IsRevoked = true;
            RevokedAt = DateTime.UtcNow;
        }

        public bool IsExpired()
        {
            return DateTime.UtcNow >= ExpiresAt;
        }
    }
}
