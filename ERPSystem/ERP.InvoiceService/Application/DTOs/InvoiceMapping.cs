using InvoiceService.Application.DTOs;
using InvoiceService.Domain;

public static class InvoiceMapping
{
    public static InvoiceDto ToDto(this Invoice invoice) => new(
        Id: invoice.Id,
        InvoiceNumber: invoice.InvoiceNumber,
        InvoiceDate: invoice.InvoiceDate,
        DueDate: invoice.DueDate,
        TotalHT: Math.Round(invoice.TotalHT, 2),
        TotalTVA: Math.Round(invoice.TotalTVA, 2),
        TotalTTC: Math.Round(invoice.TotalTTC, 2),
        TaxMode: invoice.TaxCalculationMode,
        DiscountRate: Math.Round(invoice.DiscountRate, 2),
        Status: invoice.Status.ToString(),
        ClientId: invoice.ClientId,
        ClientFullName: invoice.ClientFullName,
        ClientAddress: invoice.ClientAddress,
        AdditionalNotes: invoice.AdditionalNotes,
        IsDeleted: invoice.IsDeleted,
        CreatedAt: invoice.CreatedAt,
        UpdatedAt: invoice.UpdatedAt,
        Items: invoice.Items.Select(i => i.ToDto()).ToList(),
        TenantId: invoice.TenantId
    );

    public static InvoiceItemDto ToDto(this InvoiceItem item) => new(
        Id: item.Id,
        ArticleId: item.ArticleId,
        ArticleName: item.ArticleName,
        ArticleBarCode: item.ArticleBarCode,
        Quantity: Math.Round(item.Quantity, 3), // quantity keeps 3 decimals (kg, L, etc.)
        UniPriceHT: Math.Round(item.UniPriceHT, 2),
        EffectivePriceHT: Math.Round(item.EffectivePriceHT, 2),
        TaxRate: Math.Round(item.TaxRate, 4), // rate stored as 0.0000–1.0000
        TotalHT: Math.Round(item.TotalHT, 2),
        TotalTTC: Math.Round(item.TotalTTC, 2)
    );
}