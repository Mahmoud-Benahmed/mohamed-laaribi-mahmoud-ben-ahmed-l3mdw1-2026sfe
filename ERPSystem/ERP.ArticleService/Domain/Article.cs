namespace ERP.ArticleService.Domain
{
    public class Article
    {
        public Guid Id { get; private set; }
        public Guid? TenantId { get; init; }
        public Guid CategoryId { get; private set; }
        public Category Category { get; private set; }

        public string CodeRef { get; init; }
        public string BarCode { get; private set; }

        public string Libelle { get; private set; }
        public decimal Prix { get; private set; }
        public int TVA { get; private set; }
        public UnitEnum Unit { get; private set; }

        public bool IsDeleted { get; private set; } = false;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }


        private Article() { }

        public Article(string code, string libelle, decimal prix, UnitEnum unit, Category category, string barCode, int? tva, Guid? tenantId=null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required");

            if (string.IsNullOrWhiteSpace(libelle))
                throw new ArgumentException("Libelle is required");

            if (prix <= 0)
                throw new ArgumentException("Prix must be positive");

            int resolvedTVA = tva ?? category.TVA;
            if (resolvedTVA < 0)
                throw new ArgumentException("TVA must be greater or equal to zero.");

            Id = Guid.NewGuid();
            TenantId= tenantId;
            CodeRef = code;
            Libelle = libelle.Trim();
            Prix = Math.Round(prix, 2);
            Unit = unit;
            Category = category ?? throw new ArgumentException("Category is required");
            CategoryId = category.Id;
            BarCode = barCode;
            TVA = resolvedTVA;
            CreatedAt = DateTime.UtcNow;
        }

        public void Update(string libelle, decimal prix, UnitEnum unit, Category category, string barCode, int? tva)
        {
            if (string.IsNullOrWhiteSpace(libelle))
                throw new ArgumentException("Libelle is required");

            if (string.IsNullOrWhiteSpace(barCode))
                throw new ArgumentException("Code is required");

            if (prix <= 0)
                throw new ArgumentException("Prix must be positive");

            int resolvedTVA = tva ?? category.TVA;
            if (resolvedTVA <= 0)
                throw new ArgumentException("TVA must be greater than zero.");

            bool hasChanged = unit != Unit
                            || !string.Equals(libelle, Libelle, StringComparison.OrdinalIgnoreCase)
                            || prix != Prix
                            || category.Id != CategoryId
                            || !string.Equals(barCode, BarCode, StringComparison.OrdinalIgnoreCase)
                            || resolvedTVA != TVA;

            if (!hasChanged) return;

            Libelle = libelle.Trim();
            Prix = Math.Round(prix, 2);
            Unit = unit;
            Category = category;
            CategoryId = category.Id;
            BarCode = barCode;
            TVA = resolvedTVA;
            UpdatedAt = DateTime.UtcNow;
        }


        public void Delete()
        {
            if (IsDeleted) return;
            IsDeleted = true;

        }

        public void Restore()
        {
            if (!IsDeleted) return;
            IsDeleted = false;

        }
    }
}

public enum UnitEnum
{
    // ── Pieces / Countable Items ─────────────────────────
    Piece,          // single item

    // -- Weight
    Gram,
    Kilogram,
    Milligram,
    Ton,            // metric ton

    // ── Volume Units ──────────────────────────────────
    Milliliter,
    Liter,
    CubicMeter,     // e.g., liquids, bulk materials

    // ── Length / Distance Units ───────────────────────
    Millimeter,
    Centimeter,
    Meter,
    Kilometer,

    // ── Misc / Special Units ─────────────────────────
    Hour,           // used for labor tracking
    Day            // rental periods or project duration
}