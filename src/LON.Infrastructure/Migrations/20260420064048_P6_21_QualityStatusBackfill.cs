using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <summary>
    /// P6.21 — data-only migration. Backfills the legacy default `QualityStatus = 0`
    /// (CLR default of the enum, previously unlabelled) to `1 = OK` on every row
    /// produced before creation paths were taught to coerce the unset value. Without
    /// this pass, MaterialIssue / Export / Waste resolvers skip those balances even
    /// though GET /api/wms/inventory surfaces them.
    ///
    /// Safe because `0` was never an intended value: the enum only defines OK=1,
    /// Blocked=2, Quarantine=3. Anything persisted as 0 is a JSON-omission artefact.
    /// </summary>
    public partial class P6_21_QualityStatusBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [InventoryBalances] SET [QualityStatus] = 1 WHERE [QualityStatus] = 0;");
            migrationBuilder.Sql("UPDATE [ReceiptLines] SET [QualityStatus] = 1 WHERE [QualityStatus] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the original 0 values were always a bug. No
            // meaningful rollback.
        }
    }
}
