namespace ERP.StockService.Domain
{
    public sealed class JournalStock
    {
        public Guid Id { get; private set; }
        public Guid? TenantId { get; private set; }

        // references
        public Guid ArticleId { get; private set; }
        public Guid LigneId { get; private set; }
        public Guid PieceId { get; private set; }

        // movement
        public decimal Quantity { get; private set; }

        // before/after tracking (VERY useful)
        public decimal StockBefore { get; private set; }
        public decimal StockAfter { get; private set; }

        // operation classification
        public StockMovementType MovementType { get; private set; }

        // traceability
        public string SourceService { get; private set; }
        public string SourceOperation { get; private set; }

        // audit
        public DateTime CreatedAt { get; private set; }
        public Guid? PerformedBy { get; private set; }

        private JournalStock() { }

        public static JournalStock Create(
            Guid articleId,
            Guid ligneId,
            Guid pieceId,
            decimal quantity,
            decimal stockBefore,
            StockMovementType movementType,
            string sourceService,
            string sourceOperation = "CreateBonEntre",
            Guid? performedBy = null,
            Guid? tenantId= null)
        {
            decimal stockAfter = stockBefore + quantity;

            return new JournalStock
            {
                Id = Guid.NewGuid(),
                ArticleId = articleId,
                LigneId = ligneId,
                PieceId = pieceId,
                Quantity = quantity,
                StockBefore = stockBefore,
                StockAfter = stockAfter,
                MovementType = movementType,
                SourceService = sourceService,
                SourceOperation = sourceOperation,
                CreatedAt = DateTime.UtcNow,
                PerformedBy = performedBy
            };
        }
    }
}

public enum StockMovementType
{
    BonEntre,
    BonSortie,
    BonRetour,
    InvoiceCreate,
    InvoiceCancel,
    InventoryAdjustment,
    Transfer
}