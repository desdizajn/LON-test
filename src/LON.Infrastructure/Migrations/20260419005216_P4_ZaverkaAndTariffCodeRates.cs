using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4_ZaverkaAndTariffCodeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ZaverkaDate",
                table: "CustomsDeclarations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZaverkaNumber",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TariffCodeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TariffCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomsRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffCodeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TariffCodeRates_TariffCodes_TariffCodeId",
                        column: x => x.TariffCodeId,
                        principalTable: "TariffCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TariffCodeRates_TariffCodeId_ValidFrom",
                table: "TariffCodeRates",
                columns: new[] { "TariffCodeId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TariffCodeRates_TariffCodeId_ValidTo",
                table: "TariffCodeRates",
                columns: new[] { "TariffCodeId", "ValidTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TariffCodeRates");

            migrationBuilder.DropColumn(
                name: "ZaverkaDate",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ZaverkaNumber",
                table: "CustomsDeclarations");
        }
    }
}
