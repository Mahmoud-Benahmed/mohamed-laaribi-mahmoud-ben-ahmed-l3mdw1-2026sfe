using ERP.AuthService.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;

namespace ERP.AuthService.Domain
{
    public class Role : ITenantFilterable
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; private set; }

        [BsonRepresentation(BsonType.String)]
        public string Libelle { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? TenantId { get; private set; }
        public bool IsGlobal => TenantId == null;

        private Role() { }

        public Role(string libelle, Guid? tenantId= null)
        {
            Id = Guid.NewGuid();
            Libelle = Regex.Replace(
                libelle.Trim().ToUpper(),
                @"\s+",
                "_"
            );

            TenantId = tenantId;
        }

        public void UpdateRole(string libelle)
        {
            Libelle = libelle;
        }

    }
}
