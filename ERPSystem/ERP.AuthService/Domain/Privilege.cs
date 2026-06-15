using ERP.AuthService.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ERP.AuthService.Domain
{
    public class Privilege : ITenantFilterable
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid RoleId { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid ControleId { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? TenantId { get; private set; }
        public bool IsGlobal => TenantId == null;

        public bool IsGranted { get; private set; }

        private Privilege() { }

        public Privilege(Guid roleId, Guid controleId, bool isGranted, Guid? tenantId= null)
        {
            Id = Guid.NewGuid();
            RoleId = roleId;
            ControleId = controleId;
            IsGranted = isGranted;
            TenantId = tenantId;
        }

        public void SetGranted(bool isGranted)
        {
            IsGranted = isGranted;
        }
    }
}