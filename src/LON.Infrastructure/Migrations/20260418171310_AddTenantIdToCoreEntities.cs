using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToCoreEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Receipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ReceiptLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Partners",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Locations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "InventoryMovements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "InventoryBalances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId",
                table: "Warehouses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TenantId",
                table: "Receipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_TenantId",
                table: "ReceiptLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_TenantId",
                table: "Partners",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_TenantId",
                table: "Locations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantId",
                table: "Items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId",
                table: "InventoryMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_TenantId",
                table: "InventoryBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                table: "Employees",
                column: "TenantId");

            // Backfill TenantId for all existing rows in the 10 newly-scoped tables.
            // Uses TEKSPORT as the single default tenant (seeded in P1.1). FK
            // constraints added below would otherwise reject Guid.Empty defaults.
            // If no Tenants row named TEKSPORT exists yet (very old DB), fall
            // back to the first active tenant.
            migrationBuilder.Sql(@"
DECLARE @tenantId UNIQUEIDENTIFIER =
    (SELECT TOP 1 Id FROM Tenants WHERE Code = 'TEKSPORT');
IF @tenantId IS NULL
    SET @tenantId = (SELECT TOP 1 Id FROM Tenants WHERE IsActive = 1 ORDER BY CreatedAt);

IF @tenantId IS NOT NULL
BEGIN
    UPDATE Employees         SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE InventoryBalances SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE InventoryMovements SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Items             SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Locations         SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Partners          SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE ReceiptLines      SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Receipts          SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Users             SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Warehouses        SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
END
");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Tenants_TenantId",
                table: "Employees",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryBalances_Tenants_TenantId",
                table: "InventoryBalances",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Tenants_TenantId",
                table: "InventoryMovements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Tenants_TenantId",
                table: "Items",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Tenants_TenantId",
                table: "Locations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Partners_Tenants_TenantId",
                table: "Partners",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_Tenants_TenantId",
                table: "ReceiptLines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Tenants_TenantId",
                table: "Receipts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Tenants_TenantId",
                table: "Warehouses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Tenants_TenantId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryBalances_Tenants_TenantId",
                table: "InventoryBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Tenants_TenantId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Tenants_TenantId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Tenants_TenantId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Partners_Tenants_TenantId",
                table: "Partners");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLines_Tenants_TenantId",
                table: "ReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Tenants_TenantId",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Tenants_TenantId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_TenantId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_TenantId",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_TenantId",
                table: "ReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_Partners_TenantId",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_Locations_TenantId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TenantId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_TenantId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryBalances");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Employees");
        }
    }
}
