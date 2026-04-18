using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLocationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed data persisted Type=0 for pre-existing locations (seeder enum value not captured
            // on earlier runs). Backfill by Code convention so CreateReceiptCommandHandler's
            // Type=Receiving lookup works reliably.
            migrationBuilder.Sql("UPDATE Locations SET Type = 1 WHERE Code LIKE 'RCV%'  AND Type = 0;"); // Receiving
            migrationBuilder.Sql("UPDATE Locations SET Type = 2 WHERE Code LIKE 'STG%'  AND Type = 0;"); // Storage
            migrationBuilder.Sql("UPDATE Locations SET Type = 3 WHERE Code LIKE 'PICK%' AND Type = 0;"); // Picking
            migrationBuilder.Sql("UPDATE Locations SET Type = 4 WHERE Code LIKE 'PROD%' AND Type = 0;"); // Production
            migrationBuilder.Sql("UPDATE Locations SET Type = 5 WHERE Code LIKE 'SHIP%' AND Type = 0;"); // Shipping
            migrationBuilder.Sql("UPDATE Locations SET Type = 6 WHERE Code LIKE 'QUA%'  AND Type = 0;"); // Quarantine
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible (we don't remember which rows were null); safe to no-op.
        }
    }
}
