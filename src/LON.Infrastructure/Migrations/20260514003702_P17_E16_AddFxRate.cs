using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E16_AddFxRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ToCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FxRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_Lookup",
                table: "FxRates",
                columns: new[] { "TenantId", "FromCurrency", "ToCurrency", "EffectiveDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_TenantId",
                table: "FxRates",
                column: "TenantId");

            // Phase 17 §E16 — seed today's EUR/MKD + USD/MKD + USD/EUR for every
            // active tenant. Values are placeholders the user updates after
            // deploy (BLUEPRINT §5.14.8: manual import for v1; auto-import is
            // Phase 27.1). Idempotent: skips if any row already exists for the
            // (tenant, fromCcy, toCcy) pair.
            migrationBuilder.Sql(@"
                DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);
                DECLARE @tenantId UNIQUEIDENTIFIER;
                DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT Id FROM Tenants WHERE IsActive = 1 AND IsDeleted = 0;
                OPEN tcur;
                FETCH NEXT FROM tcur INTO @tenantId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM FxRates WHERE TenantId=@tenantId AND FromCurrency='EUR' AND ToCurrency='MKD')
                        INSERT INTO FxRates (Id, TenantId, FromCurrency, ToCurrency, Rate, EffectiveDate, Source, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'EUR', 'MKD', 61.50, @today, 1, SYSUTCDATETIME(), 'P17.E16', 0);
                    IF NOT EXISTS (SELECT 1 FROM FxRates WHERE TenantId=@tenantId AND FromCurrency='USD' AND ToCurrency='MKD')
                        INSERT INTO FxRates (Id, TenantId, FromCurrency, ToCurrency, Rate, EffectiveDate, Source, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'USD', 'MKD', 56.00, @today, 1, SYSUTCDATETIME(), 'P17.E16', 0);
                    IF NOT EXISTS (SELECT 1 FROM FxRates WHERE TenantId=@tenantId AND FromCurrency='USD' AND ToCurrency='EUR')
                        INSERT INTO FxRates (Id, TenantId, FromCurrency, ToCurrency, Rate, EffectiveDate, Source, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'USD', 'EUR', 0.91, @today, 1, SYSUTCDATETIME(), 'P17.E16', 0);
                    FETCH NEXT FROM tcur INTO @tenantId;
                END
                CLOSE tcur; DEALLOCATE tcur;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRates");
        }
    }
}
