using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15_5_GuaranteeBalanceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuaranteeBalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuaranteeAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DebitedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreditedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NetBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AvailableLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActiveDebitCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuaranteeBalanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuaranteeBalanceSnapshots_GuaranteeAccounts_GuaranteeAccountId",
                        column: x => x.GuaranteeAccountId,
                        principalTable: "GuaranteeAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuaranteeBalanceSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeBalanceSnapshots_GuaranteeAccountId_SnapshotDate",
                table: "GuaranteeBalanceSnapshots",
                columns: new[] { "GuaranteeAccountId", "SnapshotDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeBalanceSnapshots_SnapshotDate",
                table: "GuaranteeBalanceSnapshots",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeBalanceSnapshots_TenantId",
                table: "GuaranteeBalanceSnapshots",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuaranteeBalanceSnapshots");
        }
    }
}
