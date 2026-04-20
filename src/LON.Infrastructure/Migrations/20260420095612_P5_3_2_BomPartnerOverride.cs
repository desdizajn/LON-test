using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P5_3_2_BomPartnerOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartnerId",
                table: "BOMs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOMs_PartnerId",
                table: "BOMs",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_BOMs_Partners_PartnerId",
                table: "BOMs",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BOMs_Partners_PartnerId",
                table: "BOMs");

            migrationBuilder.DropIndex(
                name: "IX_BOMs_PartnerId",
                table: "BOMs");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "BOMs");
        }
    }
}
