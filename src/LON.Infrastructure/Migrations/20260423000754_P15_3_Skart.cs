using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_3_Skart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Skarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkartNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MRN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SkartQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UoMId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Resolution = table.Column<int>(type: "int", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skarts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skarts_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Skarts_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalTable: "ReceiptLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Skarts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Skarts_UnitsOfMeasure_UoMId",
                        column: x => x.UoMId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_ItemId",
                table: "Skarts",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_ReceiptLineId",
                table: "Skarts",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_ReportedAt",
                table: "Skarts",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_Resolution",
                table: "Skarts",
                column: "Resolution");

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_TenantId",
                table: "Skarts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_TenantId_SkartNumber",
                table: "Skarts",
                columns: new[] { "TenantId", "SkartNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Skarts_UoMId",
                table: "Skarts",
                column: "UoMId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Skarts");
        }
    }
}
