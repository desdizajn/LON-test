using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E8_5_AddCommercialInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommercialInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClientOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomsDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsigneePartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsignorPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CountryOfDestination = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Incoterms = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommercialInvoices_ClientOrders_ClientOrderId",
                        column: x => x.ClientOrderId,
                        principalTable: "ClientOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoices_CustomsDeclarations_CustomsDeclarationId",
                        column: x => x.CustomsDeclarationId,
                        principalTable: "CustomsDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoices_Partners_ConsigneePartnerId",
                        column: x => x.ConsigneePartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoices_Partners_ConsignorPartnerId",
                        column: x => x.ConsignorPartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoices_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommercialInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommercialInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UoMId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    TariffCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommercialInvoiceLines_CommercialInvoices_CommercialInvoiceId",
                        column: x => x.CommercialInvoiceId,
                        principalTable: "CommercialInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommercialInvoiceLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoiceLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialInvoiceLines_UnitsOfMeasure_UoMId",
                        column: x => x.UoMId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoiceLines_CommercialInvoiceId",
                table: "CommercialInvoiceLines",
                column: "CommercialInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoiceLines_ItemId",
                table: "CommercialInvoiceLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoiceLines_TenantId",
                table: "CommercialInvoiceLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoiceLines_UoMId",
                table: "CommercialInvoiceLines",
                column: "UoMId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_ClientOrderId",
                table: "CommercialInvoices",
                column: "ClientOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_ConsigneePartnerId",
                table: "CommercialInvoices",
                column: "ConsigneePartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_ConsignorPartnerId",
                table: "CommercialInvoices",
                column: "ConsignorPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_CustomsDeclarationId",
                table: "CommercialInvoices",
                column: "CustomsDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_ShipmentId",
                table: "CommercialInvoices",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_TenantId",
                table: "CommercialInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_TenantId_ClientOrderId",
                table: "CommercialInvoices",
                columns: new[] { "TenantId", "ClientOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_TenantId_InvoiceDate",
                table: "CommercialInvoices",
                columns: new[] { "TenantId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialInvoices_TenantId_Number",
                table: "CommercialInvoices",
                columns: new[] { "TenantId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Phase 17 §E8.5 — per-tenant SQL SEQUENCE for CI-{year}-{seq:D6}
            // numbering (same pattern as §E1 ClientOrder, §E7.6 DeliveryNote).
            migrationBuilder.Sql(@"
                DECLARE @tenantId UNIQUEIDENTIFIER;
                DECLARE @seqName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);
                DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT Id FROM Tenants WHERE IsActive = 1 AND IsDeleted = 0;
                OPEN tcur;
                FETCH NEXT FROM tcur INTO @tenantId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @seqName = 'seq_CommercialInvoice_' + REPLACE(CAST(@tenantId AS NVARCHAR(50)), '-', '');
                    IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = @seqName)
                    BEGIN
                        SET @sql = N'CREATE SEQUENCE ' + QUOTENAME(@seqName)
                                 + N' AS bigint START WITH 1 INCREMENT BY 1 NO CACHE;';
                        EXEC sp_executesql @sql;
                    END

                    FETCH NEXT FROM tcur INTO @tenantId;
                END
                CLOSE tcur; DEALLOCATE tcur;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @seqName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);
                DECLARE scur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT name FROM sys.sequences WHERE name LIKE 'seq_CommercialInvoice_%';
                OPEN scur;
                FETCH NEXT FROM scur INTO @seqName;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = N'DROP SEQUENCE ' + QUOTENAME(@seqName) + N';';
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM scur INTO @seqName;
                END
                CLOSE scur; DEALLOCATE scur;
            ");

            migrationBuilder.DropTable(
                name: "CommercialInvoiceLines");

            migrationBuilder.DropTable(
                name: "CommercialInvoices");
        }
    }
}
