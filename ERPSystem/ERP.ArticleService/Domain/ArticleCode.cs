namespace ERP.ArticleService.Domain
{
    public class ArticleCode
    {
        public Guid Id { get; private set; }
        public string Prefix { get; private set; }
        public int LastNumber { get; private set; }
        public int Padding { get; private set; }
        public Guid? TenantId { get; init; }

        // Required by EF Core for materialization
        private ArticleCode() { }

        public ArticleCode(string prefix, Guid? tenantId=null, int padding = 6)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix cannot be empty.", nameof(prefix));
            if (padding <= 0)
                throw new ArgumentOutOfRangeException(nameof(padding), "Padding must be greater than zero.");

            Id = Guid.NewGuid();
            TenantId = tenantId;
            Padding = padding;
            Prefix = !string.IsNullOrEmpty(prefix)
                ? $"{prefix.Trim().ToUpperInvariant()}-ART"
                : "ART";
            LastNumber = 0;
        }

        /// <summary>
        /// Increments the sequence. Called only by ArticleCodeRepository
        /// inside a locked transaction.
        /// </summary>
        public void Increment() => LastNumber++;
        public void Decrement() => LastNumber--;

        /// <summary>
        /// Formats the code as: {Prefix}-{Year}-{LastNumber padded to Padding digits}
        /// e.g. Prefix="ART", Year=2026, LastNumber=42, Padding=6 → "ART-2026-000042"
        /// </summary>
        public string FormatCode(int year) =>
            $"{Prefix}-{year}-{LastNumber.ToString().PadLeft(Padding, '0')}";
    }
}