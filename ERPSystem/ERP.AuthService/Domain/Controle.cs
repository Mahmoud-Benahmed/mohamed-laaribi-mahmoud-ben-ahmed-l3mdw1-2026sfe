using ERP.AuthService.Application.DTOs.Role;
using ERP.AuthService.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;

namespace ERP.AuthService.Domain
{
    public class Controle:ITenantFilterable
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; private set; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? TenantId { get; private set; }

        public string Category { get; private set; } = default!;
        public string Libelle { get; private set; } = default!;
        public string Description { get; private set; } = default!;

        [BsonConstructor]
        private Controle() { }

        public Controle(string category, string libelle, string description, Guid? tenantId= null)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            Category = Regex.Replace(
                category.Trim().ToUpper(),
                @"\s+",
                "_"
            );
            Libelle = Regex.Replace(
                libelle.Trim().ToUpper(),
                @"\s+",
                "_"
            );
            Description = description.Trim();
        }



        public void Update(ControleRequestDto controle)
        {
            if (equals(controle))
                return;
            Category = string.IsNullOrWhiteSpace(controle.Category) ? Category : controle.Category;
            Libelle = string.IsNullOrWhiteSpace(controle.Libelle) ? Libelle : controle.Libelle;
            Description = string.IsNullOrWhiteSpace(controle.Description) ? Description : controle.Description;
        }

        private bool equals(ControleRequestDto controle)
        {
            return controle.Description.Equals(Description, StringComparison.OrdinalIgnoreCase)
                && controle.Libelle.Equals(Libelle, StringComparison.OrdinalIgnoreCase)
                && controle.Category.Equals(Category, StringComparison.OrdinalIgnoreCase);
        }
    }
}
