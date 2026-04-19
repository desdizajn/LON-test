using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KW12_ColorSizeParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CustomerOrderNumber",
                table: "ProductionOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainOrderNumber",
                table: "ProductionOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentOrderId",
                table: "ProductionOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubOrderNumber",
                table: "ProductionOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseCode",
                table: "Items",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorCode",
                table: "Items",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentItemId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeCode",
                table: "Items",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ParentOrderId",
                table: "ProductionOrders",
                column: "ParentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_MainOrderNumber",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "MainOrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_ParentItemId",
                table: "Items",
                column: "ParentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantId_BaseCode",
                table: "Items",
                columns: new[] { "TenantId", "BaseCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_ParentItemId",
                table: "Items",
                column: "ParentItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_ProductionOrders_ParentOrderId",
                table: "ProductionOrders",
                column: "ParentOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_ParentItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_ProductionOrders_ParentOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_ParentOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_TenantId_MainOrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_Items_ParentItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantId_BaseCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MainOrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ParentOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SubOrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "BaseCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ColorCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ParentItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SizeCode",
                table: "Items");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerOrderNumber",
                table: "ProductionOrders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
