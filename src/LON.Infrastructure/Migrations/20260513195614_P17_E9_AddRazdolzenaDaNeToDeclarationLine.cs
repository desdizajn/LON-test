using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E9_AddRazdolzenaDaNeToDeclarationLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RazdolzenaAt",
                table: "CustomsDeclarationLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazdolzenaBy",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RazdolzenaDaNe",
                table: "CustomsDeclarationLines",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RazdolzenaAt",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "RazdolzenaBy",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "RazdolzenaDaNe",
                table: "CustomsDeclarationLines");
        }
    }
}
