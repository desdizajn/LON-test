using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P26a_AddDischargedQuantityToMRNRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DischargedQuantity",
                table: "MRNRegistries",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            // Seed the "3151" re-export procedure on existing deployments whose
            // CustomsProcedures table was populated before this procedure was
            // added to SeedCustomsProcedures. Guarded by NOT EXISTS so idempotent.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [CustomsProcedures] WHERE [Code] = '3151')
BEGIN
    INSERT INTO [CustomsProcedures]
        ([Id], [Code], [Name], [Type], [Description], [RequiresGuarantee],
         [GuaranteePercentage], [DueDays], [RequiresMRNTracking],
         [AllowsProduction], [AllowsExport], [IsActive], [CreatedAt],
         [CreatedBy], [IsDeleted])
    VALUES
        (NEWID(), '3151', 'Re-export of LON goods (31 51)', 5,
         'Export after inward processing — discharges LON bond (MK Правилник)',
         0, 0, NULL, 1, 0, 1, 1, SYSUTCDATETIME(), 'P26a-Migration', 0);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DischargedQuantity",
                table: "MRNRegistries");
        }
    }
}
