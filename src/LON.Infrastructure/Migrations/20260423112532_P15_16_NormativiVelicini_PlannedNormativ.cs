using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_16_NormativiVelicini_PlannedNormativ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSizeBreakdown",
                table: "ProductionOrderMaterials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedQuantityPerUnit",
                table: "ProductionOrderMaterials",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductionOrderMaterialSizes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionOrderMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SizeOrdinal = table.Column<int>(type: "int", nullable: false),
                    SizeLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NormativPerUnit = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TotalMaterialQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderMaterialSizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderMaterialSizes_ProductionOrderMaterials_ProductionOrderMaterialId",
                        column: x => x.ProductionOrderMaterialId,
                        principalTable: "ProductionOrderMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionOrderMaterialSizes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterialSizes_ProductionOrderMaterialId_SizeOrdinal",
                table: "ProductionOrderMaterialSizes",
                columns: new[] { "ProductionOrderMaterialId", "SizeOrdinal" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterialSizes_TenantId",
                table: "ProductionOrderMaterialSizes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionOrderMaterialSizes");

            migrationBuilder.DropColumn(
                name: "HasSizeBreakdown",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "PlannedQuantityPerUnit",
                table: "ProductionOrderMaterials");
        }
    }
}
