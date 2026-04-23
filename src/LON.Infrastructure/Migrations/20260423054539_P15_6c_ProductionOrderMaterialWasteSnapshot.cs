using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_6c_ProductionOrderMaterialWasteSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryWasteItemId",
                table: "ProductionOrderMaterials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimaryWastePercentage",
                table: "ProductionOrderMaterials",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecondaryWasteItemId",
                table: "ProductionOrderMaterials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondaryWastePercentage",
                table: "ProductionOrderMaterials",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TertiaryWasteItemId",
                table: "ProductionOrderMaterials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TertiaryWastePercentage",
                table: "ProductionOrderMaterials",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZagubaItemId",
                table: "ProductionOrderMaterials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZagubaPercentage",
                table: "ProductionOrderMaterials",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_PrimaryWasteItemId",
                table: "ProductionOrderMaterials",
                column: "PrimaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_SecondaryWasteItemId",
                table: "ProductionOrderMaterials",
                column: "SecondaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_TertiaryWasteItemId",
                table: "ProductionOrderMaterials",
                column: "TertiaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_ZagubaItemId",
                table: "ProductionOrderMaterials",
                column: "ZagubaItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderMaterials_Items_PrimaryWasteItemId",
                table: "ProductionOrderMaterials",
                column: "PrimaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderMaterials_Items_SecondaryWasteItemId",
                table: "ProductionOrderMaterials",
                column: "SecondaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderMaterials_Items_TertiaryWasteItemId",
                table: "ProductionOrderMaterials",
                column: "TertiaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderMaterials_Items_ZagubaItemId",
                table: "ProductionOrderMaterials",
                column: "ZagubaItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderMaterials_Items_PrimaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderMaterials_Items_SecondaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderMaterials_Items_TertiaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderMaterials_Items_ZagubaItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_PrimaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_SecondaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_TertiaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_ZagubaItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "PrimaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "PrimaryWastePercentage",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "SecondaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "SecondaryWastePercentage",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "TertiaryWasteItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "TertiaryWastePercentage",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "ZagubaItemId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "ZagubaPercentage",
                table: "ProductionOrderMaterials");
        }
    }
}
