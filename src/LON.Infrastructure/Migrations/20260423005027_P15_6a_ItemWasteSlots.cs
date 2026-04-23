using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_6a_ItemWasteSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PartnerSKU",
                table: "Items",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWasteCatalog",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryWasteItemId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimaryWastePercentage",
                table: "Items",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecondaryWasteItemId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondaryWastePercentage",
                table: "Items",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TertiaryWasteItemId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TertiaryWastePercentage",
                table: "Items",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WasteTariffCode",
                table: "Items",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZagubaItemId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZagubaPercentage",
                table: "Items",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_PrimaryWasteItemId",
                table: "Items",
                column: "PrimaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SecondaryWasteItemId",
                table: "Items",
                column: "SecondaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantId_PartnerSKU",
                table: "Items",
                columns: new[] { "TenantId", "PartnerSKU" },
                filter: "[PartnerSKU] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TertiaryWasteItemId",
                table: "Items",
                column: "TertiaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ZagubaItemId",
                table: "Items",
                column: "ZagubaItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_PrimaryWasteItemId",
                table: "Items",
                column: "PrimaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_SecondaryWasteItemId",
                table: "Items",
                column: "SecondaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_TertiaryWasteItemId",
                table: "Items",
                column: "TertiaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_ZagubaItemId",
                table: "Items",
                column: "ZagubaItemId",
                principalTable: "Items",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_PrimaryWasteItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_SecondaryWasteItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_TertiaryWasteItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_ZagubaItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_PrimaryWasteItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SecondaryWasteItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantId_PartnerSKU",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_TertiaryWasteItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ZagubaItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsWasteCatalog",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PrimaryWasteItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PrimaryWastePercentage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SecondaryWasteItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SecondaryWastePercentage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TertiaryWasteItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TertiaryWastePercentage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "WasteTariffCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ZagubaItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ZagubaPercentage",
                table: "Items");

            migrationBuilder.AlterColumn<string>(
                name: "PartnerSKU",
                table: "Items",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
