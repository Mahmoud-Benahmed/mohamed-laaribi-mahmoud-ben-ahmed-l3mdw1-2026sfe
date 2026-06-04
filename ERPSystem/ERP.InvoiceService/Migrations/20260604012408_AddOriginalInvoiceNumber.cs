using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.InvoiceService.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalInvoiceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalInvoiceNumber",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalInvoiceNumber",
                table: "Invoices");
        }
    }
}
