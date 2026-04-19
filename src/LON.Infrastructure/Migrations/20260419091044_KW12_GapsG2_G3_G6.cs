using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KW12_GapsG2_G3_G6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerOrderNumber",
                table: "ProductionOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPartnerId",
                table: "ProductionOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekNumber",
                table: "ProductionOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EfficiencyFactor",
                table: "ProductionOrderMaterials",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreAssignedBatchNumber",
                table: "ProductionOrderMaterials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreAssignedMRN",
                table: "ProductionOrderMaterials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreferentialOrigin",
                table: "CustomsDeclarationLines",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CustomerPartnerId",
                table: "ProductionOrders",
                column: "CustomerPartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Partners_CustomerPartnerId",
                table: "ProductionOrders",
                column: "CustomerPartnerId",
                principalTable: "Partners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Partners_CustomerPartnerId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_CustomerPartnerId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "CustomerOrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "CustomerPartnerId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "WeekNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "EfficiencyFactor",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "PreAssignedBatchNumber",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "PreAssignedMRN",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "IsPreferentialOrigin",
                table: "CustomsDeclarationLines");
        }
    }
}
