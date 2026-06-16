using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.StockService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleCategoryCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TVA = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleCategoryCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonEntres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FournisseurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonEntres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    Padding = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonNumbers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonRetours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Motif = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonRetours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonSorties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonSorties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DuePaymentPeriod = table.Column<int>(type: "int", nullable: false),
                    TaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DelaiRetour = table.Column<int>(type: "int", nullable: true),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientCategoryCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DelaiRetour = table.Column<int>(type: "int", nullable: false),
                    DuePaymentPeriod = table.Column<int>(type: "int", nullable: false),
                    DiscountRate = table.Column<decimal>(type: "decimal(5,3)", precision: 5, scale: 3, nullable: true),
                    CreditLimitMultiplier = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    UseBulkPricing = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCategoryCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FournisseurCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RIB = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FournisseurCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceBonSortieMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BonSortieId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceBonSortieMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LigneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PieceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    StockBefore = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    StockAfter = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceService = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceOperation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalStocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BarCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Prix = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TVA = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleCache_ArticleCategoryCache_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ArticleCategoryCache",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LigneEntres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BonEntreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigneEntres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigneEntres_BonEntres_BonEntreId",
                        column: x => x.BonEntreId,
                        principalTable: "BonEntres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LigneRetours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BonRetourId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarque = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigneRetours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigneRetours_BonRetours_BonRetourId",
                        column: x => x.BonRetourId,
                        principalTable: "BonRetours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LigneSorties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BonSortieId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigneSorties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigneSorties_BonSorties_BonSortieId",
                        column: x => x.BonSortieId,
                        principalTable: "BonSorties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientCategoriesCache",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCategoriesCache", x => new { x.ClientId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ClientCategoriesCache_ClientCache_ClientId",
                        column: x => x.ClientId,
                        principalTable: "ClientCache",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientCategoriesCache_ClientCategoryCache_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ClientCategoryCache",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCache_CategoryId",
                table: "ArticleCache",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCache_TenantId_BarCode",
                table: "ArticleCache",
                columns: new[] { "TenantId", "BarCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCache_TenantId_CategoryId",
                table: "ArticleCache",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCache_TenantId_CodeRef",
                table: "ArticleCache",
                columns: new[] { "TenantId", "CodeRef" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCache_TenantId_IsDeleted",
                table: "ArticleCache",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCategoryCache_TenantId_Name",
                table: "ArticleCategoryCache",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BonEntres_Numero",
                table: "BonEntres",
                columns: new[] { "Numero", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BonRetours_Numero",
                table: "BonRetours",
                columns: new[] { "Numero", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BonSorties_Numero",
                table: "BonSorties",
                columns: new[] { "Numero", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCache_TenantId_Email",
                table: "ClientCache",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCache_TenantId_IsBlocked",
                table: "ClientCache",
                columns: new[] { "TenantId", "IsBlocked" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientCategoriesCache_CategoryId",
                table: "ClientCategoriesCache",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCategoryCache_TenantId_Code",
                table: "ClientCategoryCache",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCategoryCache_TenantId_IsActive",
                table: "ClientCategoryCache",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FournisseurCaches_Email_TenantId",
                table: "FournisseurCaches",
                columns: new[] { "Email", "TenantId" },
                filter: "[Email] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FournisseurCaches_Name_TenantId",
                table: "FournisseurCaches",
                columns: new[] { "Name", "TenantId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FournisseurCaches_Phone_TenantId",
                table: "FournisseurCaches",
                columns: new[] { "Phone", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FournisseurCaches_TaxNumber",
                table: "FournisseurCaches",
                columns: new[] { "TaxNumber", "TenantId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TaxNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBonSortieMappings_BonSortieId_TenantId",
                table: "InvoiceBonSortieMappings",
                columns: new[] { "BonSortieId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBonSortieMappings_InvoiceId_TenantId",
                table: "InvoiceBonSortieMappings",
                columns: new[] { "InvoiceId", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalStocks_ArticleId",
                table: "JournalStocks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalStocks_CreatedAt",
                table: "JournalStocks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JournalStocks_MovementType",
                table: "JournalStocks",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_LigneEntres_BonEntreId",
                table: "LigneEntres",
                column: "BonEntreId");

            migrationBuilder.CreateIndex(
                name: "IX_LigneRetours_BonRetourId",
                table: "LigneRetours",
                column: "BonRetourId");

            migrationBuilder.CreateIndex(
                name: "IX_LigneSorties_BonSortieId",
                table: "LigneSorties",
                column: "BonSortieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleCache");

            migrationBuilder.DropTable(
                name: "BonNumbers");

            migrationBuilder.DropTable(
                name: "ClientCategoriesCache");

            migrationBuilder.DropTable(
                name: "FournisseurCaches");

            migrationBuilder.DropTable(
                name: "InvoiceBonSortieMappings");

            migrationBuilder.DropTable(
                name: "JournalStocks");

            migrationBuilder.DropTable(
                name: "LigneEntres");

            migrationBuilder.DropTable(
                name: "LigneRetours");

            migrationBuilder.DropTable(
                name: "LigneSorties");

            migrationBuilder.DropTable(
                name: "ArticleCategoryCache");

            migrationBuilder.DropTable(
                name: "ClientCache");

            migrationBuilder.DropTable(
                name: "ClientCategoryCache");

            migrationBuilder.DropTable(
                name: "BonEntres");

            migrationBuilder.DropTable(
                name: "BonRetours");

            migrationBuilder.DropTable(
                name: "BonSorties");
        }
    }
}
