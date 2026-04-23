using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_6b_BOMLineWasteOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryWasteItemId",
                table: "BOMLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimaryWastePercentage",
                table: "BOMLines",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecondaryWasteItemId",
                table: "BOMLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondaryWastePercentage",
                table: "BOMLines",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TertiaryWasteItemId",
                table: "BOMLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TertiaryWastePercentage",
                table: "BOMLines",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZagubaItemId",
                table: "BOMLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZagubaPercentage",
                table: "BOMLines",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_PrimaryWasteItemId",
                table: "BOMLines",
                column: "PrimaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_SecondaryWasteItemId",
                table: "BOMLines",
                column: "SecondaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_TertiaryWasteItemId",
                table: "BOMLines",
                column: "TertiaryWasteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_ZagubaItemId",
                table: "BOMLines",
                column: "ZagubaItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_BOMLines_Items_PrimaryWasteItemId",
                table: "BOMLines",
                column: "PrimaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BOMLines_Items_SecondaryWasteItemId",
                table: "BOMLines",
                column: "SecondaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BOMLines_Items_TertiaryWasteItemId",
                table: "BOMLines",
                column: "TertiaryWasteItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BOMLines_Items_ZagubaItemId",
                table: "BOMLines",
                column: "ZagubaItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BOMLines_Items_PrimaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BOMLines_Items_SecondaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BOMLines_Items_TertiaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BOMLines_Items_ZagubaItemId",
                table: "BOMLines");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_PrimaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_SecondaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_TertiaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_ZagubaItemId",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "PrimaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "PrimaryWastePercentage",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "SecondaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "SecondaryWastePercentage",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "TertiaryWasteItemId",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "TertiaryWastePercentage",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "ZagubaItemId",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "ZagubaPercentage",
                table: "BOMLines");
        }
    }
}
