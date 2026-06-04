namespace InvoiceService.Domain
{
    public class InvoiceItem
    {
        // ────────────────────────────────────────────────────────────────────────
        // PROPERTIES
        // ────────────────────────────────────────────────────────────────────────
        public Guid Id { get; private set; }
        public Guid InvoiceId { get; private set; }
        public Guid ArticleId { get; private set; }
        public string ArticleName { get; private set; }
        public string? ArticleBarCode { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UniPriceHT { get; private set; }
        public decimal EffectivePriceHT { get; private set; }
        public decimal TaxRate { get; private set; }
        public decimal TotalHT { get; private set; }
        public decimal TotalTTC { get; private set; }

        // ────────────────────────────────────────────────────────────────────────
        // CONSTRUCTORS
        // ────────────────────────────────────────────────────────────────────────
        private InvoiceItem() { }

        /// <param name="invoiceId">ID of the parent invoice</param>
        /// <param name="articleId">ID of the article</param>
        /// <param name="articleName">Name of the article</param>
        /// <param name="articleBarCode">Bar code of the article</param>
        /// <param name="quantity">Number of units (must be > 0)</param>
        /// <param name="uniPriceHT">Unit price before tax (must be >= 0)</param>
        /// <param name="taxRate">Tax rate as int (must be 0-100, e.g., 20 for 20%)</param>
        /// <exception cref="InvoiceDomainException">Thrown if validation fails</exception>
        public InvoiceItem(
            Guid invoiceId,
            Guid articleId,
            string articleLibelle,
            string articleBarCode,
            decimal quantity,
            decimal uniPriceHT,
            decimal taxRate)
        {
            // ──── VALIDATION ────

            if (quantity <= 0)
                throw new InvoiceDomainException("Quantity must be greater than zero.");

            if (uniPriceHT < 0)
                throw new InvoiceDomainException("Unit price cannot be negative.");

            if (taxRate < 0 || taxRate > 1)
                throw new InvoiceDomainException("Tax rate must be between 0 and 100.");

            // ──── INITIALIZATION ────

            Id = Guid.NewGuid();
            InvoiceId = invoiceId;
            ArticleId = articleId;
            ArticleName = articleLibelle;
            ArticleBarCode = articleBarCode;
            Quantity = Math.Round(quantity, 3, MidpointRounding.AwayFromZero); ;
            UniPriceHT = Math.Round(uniPriceHT, 3, MidpointRounding.AwayFromZero);
            TaxRate = Math.Round(taxRate, 3, MidpointRounding.AwayFromZero);

            // ──── CALCULATE TOTALS ────
            CalculateSubtotal();
        }

        // ────────────────────────────────────────────────────────────────────────
        //METHODS
        // ────────────────────────────────────────────────────────────────────────

        public void Update(decimal quantity, decimal uniPriceHT, decimal taxRate)
        {
            Quantity = Math.Round(quantity, 3, MidpointRounding.AwayFromZero);
            UniPriceHT = Math.Round(uniPriceHT, 3, MidpointRounding.AwayFromZero);
            TaxRate = Math.Round(taxRate, 3, MidpointRounding.AwayFromZero);
            CalculateSubtotal();

        }
        public void CalculateSubtotal(decimal discountRate = 0)
        {
            decimal multiplier = Math.Round(1 - (discountRate / 100m), 3, MidpointRounding.AwayFromZero); ;
            EffectivePriceHT = Math.Round(UniPriceHT * multiplier, 3, MidpointRounding.AwayFromZero); ;
            TotalHT = Math.Round(Quantity * EffectivePriceHT, 3, MidpointRounding.AwayFromZero);
            TotalTTC = Math.Round(TotalHT * (1 + TaxRate), 3, MidpointRounding.AwayFromZero); ;
        }
    }

    public enum InvoiceStatus
    {
        DRAFT,
        UNPAID,
        PAID,
        CANCELLED
    }
}