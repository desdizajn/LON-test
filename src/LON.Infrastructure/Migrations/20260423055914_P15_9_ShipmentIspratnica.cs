using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_9_ShipmentIspratnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "Shipments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShipmentRegime",
                table: "Shipments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ZaverkaDate",
                table: "Shipments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZaverkaNumber",
                table: "Shipments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ShipmentRegime",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ZaverkaDate",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ZaverkaNumber",
                table: "Shipments");
        }
    }
}
