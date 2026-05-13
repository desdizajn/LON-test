using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E1_AddClientOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientOrderId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientOrderId",
                table: "ProductionOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientOrderId",
                table: "CustomsDeclarations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustomerPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LONAuthorizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerOrderReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ClientOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientOrders_LONAuthorizations_LONAuthorizationId",
                        column: x => x.LONAuthorizationId,
                        principalTable: "LONAuthorizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientOrders_Partners_CustomerPartnerId",
                        column: x => x.CustomerPartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientOrderFinishedGoods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UoMId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BOMId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitPriceForeign = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientOrderFinishedGoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientOrderFinishedGoods_BOMs_BOMId",
                        column: x => x.BOMId,
                        principalTable: "BOMs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientOrderFinishedGoods_ClientOrders_ClientOrderId",
                        column: x => x.ClientOrderId,
                        principalTable: "ClientOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientOrderFinishedGoods_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientOrderFinishedGoods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientOrderFinishedGoods_UnitsOfMeasure_UoMId",
                        column: x => x.UoMId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_ClientOrderId",
                table: "CustomsDeclarations",
                column: "ClientOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrderFinishedGoods_BOMId",
                table: "ClientOrderFinishedGoods",
                column: "BOMId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrderFinishedGoods_ClientOrderId",
                table: "ClientOrderFinishedGoods",
                column: "ClientOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrderFinishedGoods_ItemId",
                table: "ClientOrderFinishedGoods",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrderFinishedGoods_TenantId",
                table: "ClientOrderFinishedGoods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrderFinishedGoods_UoMId",
                table: "ClientOrderFinishedGoods",
                column: "UoMId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrders_CustomerPartnerId",
                table: "ClientOrders",
                column: "CustomerPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrders_LONAuthorizationId",
                table: "ClientOrders",
                column: "LONAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrders_Status",
                table: "ClientOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrders_TenantId",
                table: "ClientOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOrders_TenantId_OrderNumber",
                table: "ClientOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomsDeclarations_ClientOrders_ClientOrderId",
                table: "CustomsDeclarations",
                column: "ClientOrderId",
                principalTable: "ClientOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Phase 17 §E1 — per-tenant SQL SEQUENCE for ClientOrder numbering.
            // Sequence name: seq_ClientOrder_<tenantId-no-dashes>.
            // One sequence per existing tenant; future tenants get one on provisioning.
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
                    SET @seqName = 'seq_ClientOrder_' + REPLACE(CAST(@tenantId AS NVARCHAR(50)), '-', '');
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
            // Drop any per-tenant ClientOrder sequences first.
            migrationBuilder.Sql(@"
                DECLARE @seqName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);
                DECLARE scur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT name FROM sys.sequences WHERE name LIKE 'seq_ClientOrder_%';
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

            migrationBuilder.DropForeignKey(
                name: "FK_CustomsDeclarations_ClientOrders_ClientOrderId",
                table: "CustomsDeclarations");

            migrationBuilder.DropTable(
                name: "ClientOrderFinishedGoods");

            migrationBuilder.DropTable(
                name: "ClientOrders");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_ClientOrderId",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                table: "CustomsDeclarations");
        }
    }
}
