using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationToReceiptLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "ReceiptLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_LocationId",
                table: "ReceiptLines",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_Locations_LocationId",
                table: "ReceiptLines",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLines_Locations_LocationId",
                table: "ReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_LocationId",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "ReceiptLines");
        }
    }
}
