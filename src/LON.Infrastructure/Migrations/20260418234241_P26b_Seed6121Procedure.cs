using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P26b_Seed6121Procedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [CustomsProcedures] WHERE [Code] = '6121')
BEGIN
    INSERT INTO [CustomsProcedures]
        ([Id], [Code], [Name], [Type], [Description], [RequiresGuarantee],
         [GuaranteePercentage], [DueDays], [RequiresMRNTracking],
         [AllowsProduction], [AllowsExport], [IsActive], [CreatedAt],
         [CreatedBy], [IsDeleted])
    VALUES
        (NEWID(), '6121', 'Re-import after export (61 21)', 3,
         'Return of previously exported LON goods — reverses EX discharge',
         0, 0, NULL, 1, 1, 1, 1, SYSUTCDATETIME(), 'P26b-Migration', 0);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [CustomsProcedures] WHERE [Code] = '6121'");
        }
    }
}
