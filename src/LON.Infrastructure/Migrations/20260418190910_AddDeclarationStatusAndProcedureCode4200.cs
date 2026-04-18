using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeclarationStatusAndProcedureCode4200 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P2.1 — add DeclarationStatus column (0=Draft, 1=Registered,
            // 2=Submitted, 3=Cleared, 99=Cancelled). Backfill existing rows:
            //   - IsCleared=1 -> Status=Cleared(3)
            //   - has MRN     -> Status=Registered(1)
            //   - else        -> Status=Draft(0)
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CustomsDeclarations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE [CustomsDeclarations]
                SET [Status] = CASE
                    WHEN [IsCleared] = 1 THEN 3
                    WHEN [MRN] IS NOT NULL AND LEN(LTRIM(RTRIM([MRN]))) > 0 THEN 1
                    ELSE 0
                END;
            ");

            // Rename the internal mnemonic 'INW-PROC' to the SAD Box 37 code
            // '4200'. Safe because declarations reference CustomsProcedureId
            // (FK), not Code. Also update the Name to the MK-localized label.
            migrationBuilder.Sql(@"
                UPDATE [CustomsProcedures]
                SET [Code] = '4200',
                    [Name] = N'Увоз за облагородување (42 00)'
                WHERE [Code] = 'INW-PROC';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [CustomsProcedures]
                SET [Code] = 'INW-PROC',
                    [Name] = N'Inward Processing'
                WHERE [Code] = '4200';
            ");
            migrationBuilder.DropColumn(
                name: "Status",
                table: "CustomsDeclarations");
        }
    }
}
