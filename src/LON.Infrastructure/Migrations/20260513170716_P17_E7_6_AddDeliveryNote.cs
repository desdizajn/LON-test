using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E7_6_AddDeliveryNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    RelatedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    VehicleRegistration = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UoMId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    MRN = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_DeliveryNoteId",
                table: "DeliveryNoteLines",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_ItemId",
                table: "DeliveryNoteLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_TenantId",
                table: "DeliveryNoteLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_RelatedDocumentId",
                table: "DeliveryNotes",
                column: "RelatedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId",
                table: "DeliveryNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId_DocumentType_DispatchDate",
                table: "DeliveryNotes",
                columns: new[] { "TenantId", "DocumentType", "DispatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TenantId_Number",
                table: "DeliveryNotes",
                columns: new[] { "TenantId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Phase 17 §E7.6 — per-tenant SQL SEQUENCE for DN-{year}-{seq:D6}
            // numbering (same pattern §E1 ClientOrder / §E3 declarations).
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
                    SET @seqName = 'seq_DeliveryNote_' + REPLACE(CAST(@tenantId AS NVARCHAR(50)), '-', '');
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
                    SELECT name FROM sys.sequences WHERE name LIKE 'seq_DeliveryNote_%';
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

            migrationBuilder.DropTable(
                name: "DeliveryNoteLines");

            migrationBuilder.DropTable(
                name: "DeliveryNotes");
        }
    }
}
