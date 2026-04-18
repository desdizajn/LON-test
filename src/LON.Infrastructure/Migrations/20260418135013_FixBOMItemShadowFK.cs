using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixBOMItemShadowFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BOMs_Items_ItemId1",
                table: "BOMs");

            migrationBuilder.DropIndex(
                name: "IX_BOMs_ItemId1",
                table: "BOMs");

            migrationBuilder.DropColumn(
                name: "ItemId1",
                table: "BOMs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ItemId1",
                table: "BOMs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOMs_ItemId1",
                table: "BOMs",
                column: "ItemId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BOMs_Items_ItemId1",
                table: "BOMs",
                column: "ItemId1",
                principalTable: "Items",
                principalColumn: "Id");
        }
    }
}
