using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Phase 17 §E3 — per-tenant SQL SEQUENCEs for IM + EX customs declarations.
    /// Names: seq_IMDeclaration_&lt;tenantId-no-dashes&gt; and
    ///        seq_EXDeclaration_&lt;tenantId-no-dashes&gt;.
    /// Same pattern as the ClientOrder sequence added in §E1.
    ///
    /// CreateCustomsDeclarationCommandHandler now auto-generates declaration
    /// numbers when the caller leaves <c>DeclarationNumber</c> blank, by
    /// calling <c>INumberSequenceService.NextAsync("IMDeclaration", tenantId)</c>
    /// or <c>"EXDeclaration"</c> depending on procedure direction.
    /// </summary>
    public partial class P17_E3_AddDeclarationSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @tenantId UNIQUEIDENTIFIER;
                DECLARE @seqName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);
                DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT Id FROM Tenants WHERE IsActive = 1 AND IsDeleted = 0;
                OPEN tcur;
                FETCH NEXT FROM tcur INTO @tenantId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @seqName = 'seq_IMDeclaration_' + REPLACE(CAST(@tenantId AS NVARCHAR(50)), '-', '');
                    IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = @seqName)
                    BEGIN
                        SET @sql = N'CREATE SEQUENCE ' + QUOTENAME(@seqName)
                                 + N' AS bigint START WITH 1 INCREMENT BY 1 NO CACHE;';
                        EXEC sp_executesql @sql;
                    END

                    SET @seqName = 'seq_EXDeclaration_' + REPLACE(CAST(@tenantId AS NVARCHAR(50)), '-', '');
                    IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = @seqName)
                    BEGIN
                        SET @sql = N'CREATE SEQUENCE ' + QUOTENAME(@seqName)
                                 + N' AS bigint START WITH 1 INCREMENT BY 1 NO CACHE;';
                        EXEC sp_executesql @sql;
                    END

                    FETCH NEXT FROM tcur INTO @tenantId;
                END
                CLOSE tcur; DEALLOCATE tcur;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @seqName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);
                DECLARE scur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT name FROM sys.sequences
                    WHERE name LIKE 'seq_IMDeclaration_%' OR name LIKE 'seq_EXDeclaration_%';
                OPEN scur;
                FETCH NEXT FROM scur INTO @seqName;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = N'DROP SEQUENCE ' + QUOTENAME(@seqName) + N';';
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM scur INTO @seqName;
                END
                CLOSE scur; DEALLOCATE scur;
            ");
        }
    }
}
