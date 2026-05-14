using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E12_AddNumberSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 17 §E12 — per-tenant SQL SEQUENCE objects for the remaining
            // numbered entities (Receipt, Shipment, MaterialIssue, ProductionOrder).
            // ClientOrder + IM/EX CustomsDeclaration + DeliveryNote +
            // CommercialInvoice already have their own sequences from earlier
            // §E migrations. GuaranteeLedgerEntry has no Number column → no
            // sequence needed.
            migrationBuilder.Sql(@"
                DECLARE @tenantId UNIQUEIDENTIFIER;
                DECLARE @seqName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);
                DECLARE @entityKeys TABLE (Name SYSNAME);
                INSERT INTO @entityKeys (Name) VALUES ('Receipt'), ('Shipment'), ('MaterialIssue'), ('ProductionOrder');

                DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT Id FROM Tenants WHERE IsActive = 1 AND IsDeleted = 0;
                OPEN tcur;
                FETCH NEXT FROM tcur INTO @tenantId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    DECLARE @k SYSNAME;
                    DECLARE kcur CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @entityKeys;
                    OPEN kcur;
                    FETCH NEXT FROM kcur INTO @k;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SET @seqName = 'seq_' + @k + '_' + REPLACE(CAST(@tenantId AS NVARCHAR(50)), '-', '');
                        IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = @seqName)
                        BEGIN
                            SET @sql = N'CREATE SEQUENCE ' + QUOTENAME(@seqName)
                                     + N' AS bigint START WITH 1 INCREMENT BY 1 NO CACHE;';
                            EXEC sp_executesql @sql;
                        END
                        FETCH NEXT FROM kcur INTO @k;
                    END
                    CLOSE kcur; DEALLOCATE kcur;
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
                    WHERE name LIKE 'seq_Receipt_%'
                       OR name LIKE 'seq_Shipment_%'
                       OR name LIKE 'seq_MaterialIssue_%'
                       OR name LIKE 'seq_ProductionOrder_%';
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
