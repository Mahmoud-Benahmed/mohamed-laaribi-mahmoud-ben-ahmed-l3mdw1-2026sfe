using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Application.Services;
using ERP.InvoiceService.Domain.LocalCache.Tenant;
using InvoiceService.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Svg;

namespace InvoiceService.Services;

public class InvoicePdfGenerator : IInvoicePdfGenerator
{
    private readonly ITenantCacheRepository _tenantCacheRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public InvoicePdfGenerator(
        ITenantCacheRepository tenantCacheRepo,
        ITenantContext tenantContext,
        IHttpClientFactory httpClientFactory)
    {
        _tenantCacheRepo = tenantCacheRepo;
        _tenantContext = tenantContext;
        _httpClientFactory = httpClientFactory;
    }

    private string CurrencySymbol = "EUR";
    private string CompanyName = "COMPANY";
    private string CompanyAddress = "123 Business Ave, City, Country";
    private string CompanyEmail = "contact@company.com";
    private string CompanyPhone = "+000 123 456 789";
    
    public async Task<byte[]> GenerateInvoicePdf(InvoiceDto invoice)
    {
        TenantCache tenant = await _tenantCacheRepo.GetByIdAsync(_tenantContext.TenantId)
            ?? throw new InvalidOperationException($"TenantCache not found for TenantId '{_tenantContext.TenantId}'. Ensure the tenant.created event was consumed.");

        CurrencySymbol = tenant.Currency    ?? CurrencySymbol;
        CompanyName = tenant.Name           ?? CompanyName;
        CompanyAddress = tenant.Address     ?? CompanyAddress;
        CompanyEmail = tenant.Email         ?? CompanyEmail;
        CompanyPhone = tenant.Phone         ?? CompanyPhone;

        Color primaryColor = HexToColor(tenant.PrimaryColor, Colors.Blue.Darken2);
        Color secondaryColor = HexToColor(tenant.SecondaryColor, Colors.Grey.Lighten2);

        Color statusColor = invoice.Status switch
        {
            "PAID" => Colors.Green.Medium,
            "UNPAID" => Colors.Orange.Medium,
            "CANCELLED" => Colors.Red.Medium,
            _ => Colors.Grey.Medium
        };

        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(tenant.LogoUrl))
        {
            if (tenant.LogoUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                // Convert SVG to PNG
                logoBytes = await ConvertSvgToPngBytesAsync(tenant.LogoUrl, 150, 80);
            }
            else
            {
                // Normal image (PNG/JPEG)
                logoBytes = await DownloadLogoAsync(tenant.LogoUrl);
            }
        }

        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                // ================= HEADER =================
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(colLeft =>
                    {
                        if (logoBytes != null)
                        {
                            // Row: logo (left) + company details (right)
                            colLeft.Item().Row(logoRow =>
                            {
                                logoRow.ConstantItem(100).Height(80).Image(logoBytes).FitArea();
                                logoRow.ConstantItem(10);
                                logoRow.RelativeItem().Column(companyCol =>
                                {
                                    companyCol.Item().Text(CompanyName)
                                        .FontSize(22).Bold().FontColor(primaryColor);
                                    companyCol.Item().Text(CompanyAddress).FontSize(9);
                                    companyCol.Item().Text($"Tel: {CompanyPhone} | Email: {CompanyEmail}").FontSize(9);
                                });
                            });
                        }
                        else
                        {
                            // Original layout without logo
                            colLeft.Item().Text(CompanyName)
                                .FontSize(22).Bold().FontColor(primaryColor);
                            colLeft.Item().Text(CompanyAddress).FontSize(9);
                            colLeft.Item().Text($"Tel: {CompanyPhone} | Email: {CompanyEmail}").FontSize(9);
                        }
                    });

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text("INVOICE")
                            .FontSize(24).Bold().FontColor(primaryColor);
                        col.Item().Text(invoice.InvoiceNumber)
                            .FontSize(14).SemiBold();
                        col.Item().Background(statusColor).PaddingVertical(5).PaddingHorizontal(10)
                            .AlignCenter()
                            .Text(invoice.Status).FontColor(Colors.White).Bold().FontSize(11);
                    });
                });

                // ================= CONTENT =================
                page.Content().PaddingVertical(20).Column(col =>
                {
                    // CLIENT + DATES
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Column(c =>
                        {
                            c.Item().Text("BILL TO").Bold().FontSize(11);
                            c.Item().Height(5);
                            c.Item().Text(invoice.ClientFullName).FontSize(10);
                            c.Item().Text(invoice.ClientAddress).FontSize(10);
                        });

                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Column(c =>
                        {
                            c.Item().Text("INVOICE DETAILS").Bold().FontSize(11);
                            c.Item().Height(5);
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Invoice Date:").Bold();
                                r.ConstantItem(100).AlignRight().Text($"{invoice.InvoiceDate:dd/MM/yyyy}");
                            });
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Due Date:").Bold();
                                r.ConstantItem(100).AlignRight().Text($"{invoice.DueDate:dd/MM/yyyy}");
                            });
                        });
                    });

                    col.Item().PaddingVertical(10);

                    // ================= TABLE =================
                    col.Item().Table(table =>
                    {
                        // 7 columns: Article, Qty, Unit Price (HT), Discount, Net Price (HT), TVA, Total TTC
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Background(secondaryColor).Padding(8).Text("Article").Bold();
                            header.Cell().Background(secondaryColor).Padding(8).Text("Qty").Bold().AlignRight();
                            header.Cell().Background(secondaryColor).Padding(8).Text("Unit Price (HT)").Bold().AlignRight();
                            header.Cell().Background(secondaryColor).Padding(8).Text("Discount").Bold().AlignCenter();
                            header.Cell().Background(secondaryColor).Padding(8).Text("Net Price (HT)").Bold().AlignRight();
                            header.Cell().Background(secondaryColor).Padding(8).Text("TVA").Bold().AlignCenter();
                            header.Cell().Background(secondaryColor).Padding(8).Text("Total TTC").Bold().AlignRight();
                        });

                        // Rows
                        int index = 0;
                        decimal discountRate = invoice.DiscountRate; // global discount percentage

                        foreach (InvoiceItemDto item in invoice.Items)
                        {
                            Color bgColor = index++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten3;
                            decimal netPriceHT = item.UniPriceHT * (1 - discountRate / 100m);
                            decimal totalTTC = item.Quantity * netPriceHT * (1 + item.TaxRate);

                            table.Cell().Background(bgColor).Padding(6).Text(item.ArticleName);
                            table.Cell().Background(bgColor).Padding(6).Text($"{item.Quantity:N2}").AlignRight();
                            table.Cell().Background(bgColor).Padding(6).Text($"{item.UniPriceHT:N2} {CurrencySymbol}").AlignRight();
                            table.Cell().Background(bgColor).Padding(6).AlignCenter().Text(discountRate > 0 ? $"{discountRate:F0}%" : "-");
                            table.Cell().Background(bgColor).Padding(6).Text($"{netPriceHT:N2} {CurrencySymbol}").AlignRight();
                            table.Cell().Background(bgColor).Padding(6).AlignCenter().Text($"{item.TaxRate * 100:F0}%");
                            table.Cell().Background(bgColor).Padding(6).Text($"{totalTTC:N2} {CurrencySymbol}").AlignRight();
                        }
                    });

                    col.Item().PaddingVertical(15);

                    // ================= TOTALS =================
                    col.Item().AlignRight().Width(260).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Column(totals =>
                    {
                        // Original subtotal (before discount)
                        decimal originalTotalHT = invoice.Items.Sum(i => i.Quantity * i.UniPriceHT);
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal (HT):").Bold();
                            r.ConstantItem(110).AlignRight().Text($"{originalTotalHT:N2} {CurrencySymbol}");
                        });

                        // Discount line (if applicable)
                        if (invoice.DiscountRate > 0)
                        {
                            decimal discountAmountHT = originalTotalHT - invoice.TotalHT;
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"Discount ({invoice.DiscountRate:F0}%):").Bold()
                                    .FontColor(Colors.Green.Darken1);
                                r.ConstantItem(110).AlignRight()
                                    .Text($"- {discountAmountHT:N2} {CurrencySymbol}")
                                    .FontColor(Colors.Green.Darken1);
                            });

                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Net HT after discount:").Bold();
                                r.ConstantItem(110).AlignRight().Text($"{invoice.TotalHT:N2} {CurrencySymbol}");
                            });
                        }

                        // TVA breakdown
                        if (invoice.TaxMode == TaxCalculationMode.INVOICE)
                        {
                            decimal effectiveRate = invoice.TotalHT > 0
                                ? (invoice.TotalTVA / invoice.TotalHT) * 100
                                : 0;
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"TVA ({effectiveRate:F2}%):").Bold();
                                r.ConstantItem(110).AlignRight().Text($"{invoice.TotalTVA:N2} {CurrencySymbol}");
                            });
                        }
                        else
                        {
                            var taxGroups = invoice.Items
                                .GroupBy(i => i.TaxRate)
                                .Select(g => new
                                {
                                    Rate = g.Key * 100,
                                    Amount = g.Sum(i => Math.Round(i.TotalHT * i.TaxRate, 2))
                                })
                                .OrderBy(g => g.Rate);

                            foreach (var group in taxGroups)
                            {
                                totals.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"TVA ({group.Rate:F0}%):").Bold();
                                    r.ConstantItem(110).AlignRight().Text($"{group.Amount:N2} {CurrencySymbol}");
                                });
                            }
                        }

                        totals.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL TTC:").Bold().FontSize(12).
                                                                        FontColor(primaryColor);
                            r.ConstantItem(110).AlignRight()
                                .Text($"{invoice.TotalTTC:N2} {CurrencySymbol}").Bold().FontSize(12)
                                                                                        .FontColor(primaryColor);
                        });

                        // ================= NOTES =================
                        if (!string.IsNullOrWhiteSpace(invoice.AdditionalNotes))
                        {
                            col.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(c =>
                            {
                                c.Item().Text("NOTES").Bold().FontSize(11);
                                c.Item().Height(5);
                                c.Item().Text(invoice.AdditionalNotes).FontSize(9);
                            });
                        }

                        // ================= PAYMENT TERMS =================
                        col.Item().PaddingTop(20).Column(terms =>
                        {
                            terms.Item().Text("Payment Terms").Bold().FontSize(10);
                            terms.Item().Text("Please pay within the due date. Bank transfer details available upon request.")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                    });

                    // ================= FOOTER =================
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" - Thank you for your business");
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static Color HexToColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return fallback;
        try
        {
            byte r = Convert.ToByte(hex[0..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return Color.FromRGB(r, g, b);
        }
        catch { return fallback; }
    }
    private async Task<byte[]?> DownloadLogoAsync(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl)) return null;
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync(logoUrl);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
            {
                // Log warning: "SVG not supported by QuestPDF, skipping logo"
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return null;
        }
    }
    public async Task<byte[]?> ConvertSvgToPngBytesAsync(string svgUrl, int maxWidth = 200, int maxHeight = 200)
    {
        byte[]? svgBytes = null;
        using var httpClient = new HttpClient();
        try
        {
            svgBytes = await httpClient.GetByteArrayAsync(svgUrl);
        }
        catch
        {
            return null; // download failed
        }

        if (svgBytes == null) return null;

        try
        {
            // Load SVG from bytes (as a string)
            var svgString = System.Text.Encoding.UTF8.GetString(svgBytes);
            var svgDocument = SvgDocument.FromSvg<SvgDocument>(svgString);

            // Option 1: Simple scaling by setting width/height (preserves aspect ratio if only one dimension changed)
            // But better to use Draw() overload that accepts target size:
            using var bitmap = svgDocument.Draw(maxWidth, maxHeight);  // scales while preserving aspect ratio

            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
            return pngStream.ToArray();
        }
        catch (Exception ex)
        {
            // Log the error (optional)
            // Console.WriteLine($"SVG conversion failed: {ex.Message}");
            return null;
        }
    }
}

public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateInvoicePdf(InvoiceDto invoice);
}

