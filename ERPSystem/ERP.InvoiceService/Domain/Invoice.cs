namespace InvoiceService.Domain
{
    public class Invoice
    {
        // ────────────────────────────────────────────────────────────────────────
        // PROPERTIES
        // ────────────────────────────────────────────────────────────────────────

        public Guid Id { get; private set; }
        public string InvoiceNumber { get; private set; }
        public TaxCalculationMode TaxCalculationMode { get; private set; }
        public DateTime InvoiceDate { get; private set; }
        public decimal DiscountRate { get; private set; }
        public DateTime DueDate { get; private set; } // last date to pay the invoice in the period between InvoiceDate and DueDate before the invoice is considered overdue and lead to create additional invoices for each period of delay
        public decimal TotalHT { get; private set; }
        public decimal TotalTVA { get; private set; }
        public decimal TotalTTC { get; private set; }
        public InvoiceStatus Status { get; private set; }
        public Guid ClientId { get; private set; }
        public string ClientFullName { get; private set; }
        public string ClientAddress { get; private set; }
        public string? AdditionalNotes { get; private set; }
        public bool IsDeleted { get; private set; } = false;
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        private readonly List<InvoiceItem> _items = new();
        public IReadOnlyCollection<InvoiceItem> Items => _items.AsReadOnly();

        // ────────────────────────────────────────────────────────────────────────
        // CONSTRUCTORS
        // ────────────────────────────────────────────────────────────────────────

        private Invoice() { }

        public Invoice(
            string invoiceNumber,
            DateTime invoiceDate,
            DateTime dueDate,
            TaxCalculationMode taxCalculation,
            decimal discountRate,
            Guid clientId,
            string clientFullName,
            string clientAddress,
            string? additionalNotes = null)
        {
            Id = Guid.NewGuid();
            InvoiceNumber = invoiceNumber;
            InvoiceDate = invoiceDate;
            TaxCalculationMode = taxCalculation;
            DiscountRate = Math.Round(discountRate, 2, MidpointRounding.AwayFromZero);
            DueDate = dueDate;
            ClientId = clientId;
            ClientFullName = clientFullName;
            ClientAddress = clientAddress;
            AdditionalNotes = additionalNotes;
            Status = InvoiceStatus.DRAFT;
            IsDeleted = false;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        // In Invoice.cs
        public Invoice CreatePenaltyInvoice(string invoiceNumber, decimal penaltyRate = 0.02m)
        {
            if (Status != InvoiceStatus.UNPAID)
                throw new InvoiceDomainException("Penalty invoices can only be created for UNPAID invoices.");

            decimal penaltyAmount = Math.Round(TotalTTC * penaltyRate, 2, MidpointRounding.AwayFromZero);

            Invoice penalty = new Invoice(
                invoiceNumber,
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(30),
                TaxCalculationMode,
                0,
                ClientId,
                ClientFullName,
                ClientAddress,
                $"Penalty invoice for overdue invoice {InvoiceNumber}");

            penalty.AddItem(new InvoiceItem(
                penalty.Id,
                Guid.Empty,              // no article — penalty line
                $"Late payment penalty ({penaltyRate * 100}%) on {InvoiceNumber}",
                null,                    // no barcode
                1,
                penaltyAmount,
                0));                     // no tax on penalty

            penalty.CalculateTotals();
            penalty.FinalizeInvoice();
            return penalty;
        }

        // ────────────────────────────────────────────────────────────────────────
        // BUSINESS METHODS
        // ────────────────────────────────────────────────────────────────────────

        /// <param name="item">The invoice item to add</param>
        /// <exception cref="InvoiceDomainException">Thrown if invoice is not in DRAFT status</exception>
        public void AddItem(InvoiceItem item)
        {
            if (Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Items can only be added to DRAFT invoices.");

            _items.Add(item);
            UpdatedAt = DateTime.UtcNow;
        }

        public void ClearItems()
        {
            _items.Clear();
        }

        /// <param name="itemId">ID of the item to remove</param>
        /// <exception cref="InvoiceDomainException">Thrown if invoice is not in DRAFT status or item not found</exception>
        public void RemoveItem(Guid itemId)
        {
            if (Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Items can only be removed from DRAFT invoices.");

            InvoiceItem item = _items.FirstOrDefault(i => i.Id == itemId)
                ?? throw new InvoiceDomainException($"Item with id '{itemId}' not found.");

            _items.Remove(item);
            UpdatedAt = DateTime.UtcNow;
        }

        public void CalculateTotals()
        {
            // Each item recalculates using the invoice-level discount
            foreach (InvoiceItem item in _items)
                item.CalculateSubtotal(DiscountRate);  // ← discount flows from invoice to items

            TotalHT = _items.Sum(i => i.TotalHT);

            if (TaxCalculationMode == TaxCalculationMode.INVOICE)
            {
                if (TotalHT == 0) { TotalTVA = 0; TotalTTC = 0; }
                else
                {
                    decimal avgRate = _items.Sum(i => i.TotalHT * i.TaxRate) / TotalHT;
                    TotalTVA = Math.Round(TotalHT * avgRate, 2, MidpointRounding.AwayFromZero);
                    TotalTTC = TotalHT + TotalTVA;
                }
            }
            else
            {
                TotalTVA = Math.Round(_items.Sum(i => i.TotalHT * i.TaxRate), 2, MidpointRounding.AwayFromZero);
                TotalTTC = Math.Round(TotalHT + TotalTVA, 2, MidpointRounding.AwayFromZero);
            }
        }

        /// <exception cref="InvoiceDomainException">Thrown if not in DRAFT status or has no items</exception>
        public void FinalizeInvoice()
        {
            if (Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Only DRAFT invoices can be finalized.");

            if (!_items.Any())
                throw new InvoiceDomainException("Cannot finalize an invoice with no items.");

            CalculateTotals();
            Status = InvoiceStatus.UNPAID;
            UpdatedAt = DateTime.UtcNow;
        }
        
        /// <exception cref="InvoiceDomainException">Thrown if not in UNPAID status</exception>
        public void MarkAsPaid()
        {
            if (Status != InvoiceStatus.UNPAID)
                throw new InvoiceDomainException("Only UNPAID invoices can be marked as paid.");

            Status = InvoiceStatus.PAID;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsUnpaid()
        {
            if (Status != InvoiceStatus.PAID)
                return;

            Status = InvoiceStatus.UNPAID;
            UpdatedAt = DateTime.UtcNow;
        }


        /// <exception cref="InvoiceDomainException">Thrown if PAID or already CANCELLED</exception>
        public void CancelInvoice()
        {
            Status = InvoiceStatus.CANCELLED;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <exception cref="InvoiceDomainException">Thrown if already deleted</exception>
        public void Delete()
        {
            if (IsDeleted)
                return;

            if(Status == InvoiceStatus.PAID || Status == InvoiceStatus.UNPAID)
                throw new InvoiceDomainException("Cannot Delete a PAID or UNPAID invoice");

            IsDeleted = true;
            UpdatedAt = DateTime.UtcNow;
        }
        /// <exception cref="InvoiceDomainException">Thrown if not deleted</exception>
        public void Restore()
        {
            if (!IsDeleted)
                return;

            if (Status == InvoiceStatus.PAID || Status == InvoiceStatus.UNPAID)
                return;

            IsDeleted = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Update(
            DateTime invoiceDate,
            DateTime dueDate,
            TaxCalculationMode taxCalculationMode,
            decimal discountRate,
            Guid clientId,
            string clientFullName,
            string clientAddress,
            string? additionalNotes)
        {
            if (Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Only DRAFT invoices can be updated.");

            TaxCalculationMode = taxCalculationMode;
            DiscountRate = Math.Round(discountRate, 2, MidpointRounding.AwayFromZero);
            InvoiceDate = invoiceDate;
            DueDate = dueDate;
            ClientId = clientId;
            ClientFullName = clientFullName;
            ClientAddress = clientAddress;
            AdditionalNotes = additionalNotes;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

public enum TaxCalculationMode
{
    LINE,
    INVOICE
}