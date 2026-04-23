using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_8_PodelbaProducerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedProducerId",
                table: "InventoryBalances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_AssignedProducerId",
                table: "InventoryBalances",
                column: "AssignedProducerId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryBalances_Partners_AssignedProducerId",
                table: "InventoryBalances",
                column: "AssignedProducerId",
                principalTable: "Partners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryBalances_Partners_AssignedProducerId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_AssignedProducerId",
                table: "InventoryBalances");

            migrationBuilder.DropColumn(
                name: "AssignedProducerId",
                table: "InventoryBalances");
        }
    }
}
