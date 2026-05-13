using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E10_5_AddAlertRulesAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameMk = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TriggerKind = table.Column<int>(type: "int", nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryChannels = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DedupKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlertEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_AlertRuleId",
                table: "AlertEvents",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_TenantId",
                table: "AlertEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_TenantId_AlertRuleId",
                table: "AlertEvents",
                columns: new[] { "TenantId", "AlertRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_TenantId_DedupKey_Status",
                table: "AlertEvents",
                columns: new[] { "TenantId", "DedupKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_TenantId_Status_OccurredAt",
                table: "AlertEvents",
                columns: new[] { "TenantId", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_TenantId",
                table: "AlertRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_TenantId_Code",
                table: "AlertRules",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_TenantId_IsActive_TriggerKind",
                table: "AlertRules",
                columns: new[] { "TenantId", "IsActive", "TriggerKind" });

            // Phase 17 §E10.5 — seed the 6 predefined rules for every active
            // tenant. Future tenant provisioning seeds the same set (handled
            // by the Tenant create flow once Phase 21 lands; for v1 every
            // tenant exists at migration time).
            migrationBuilder.Sql(@"
                DECLARE @now DATETIME2 = SYSUTCDATETIME();
                DECLARE @tenantId UNIQUEIDENTIFIER;
                DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT Id FROM Tenants WHERE IsActive = 1 AND IsDeleted = 0;
                OPEN tcur;
                FETCH NEXT FROM tcur INTO @tenantId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    -- a. Guarantee utilisation > 90%.
                    IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE TenantId = @tenantId AND Code = 'GUARANTEE_UTIL_90')
                        INSERT INTO AlertRules (Id, TenantId, Code, Name, NameMk, Severity, IsActive, TriggerKind, Threshold, RecipientsJson, DeliveryChannels, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'GUARANTEE_UTIL_90', 'Guarantee utilisation > 90%', N'Гаранцијата надмина 90%', 3, 1, 1, 0.90, '[""Administrator"",""Manager"",""FinanceOfficer""]', 'Dashboard', @now, 'P17.E10.5', 0);

                    -- b. ClientOrder due in <7 days with <50% produced.
                    IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE TenantId = @tenantId AND Code = 'ORDER_DUE_AT_RISK')
                        INSERT INTO AlertRules (Id, TenantId, Code, Name, NameMk, Severity, IsActive, TriggerKind, Threshold, RecipientsJson, DeliveryChannels, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'ORDER_DUE_AT_RISK', 'ClientOrder due <7d with <50% produced', N'Налог со рок <7д и <50% произведено', 3, 1, 2, 7.0, '[""Administrator"",""Manager""]', 'Dashboard', @now, 'P17.E10.5', 0);

                    -- c. Machine down > 2 hours.
                    IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE TenantId = @tenantId AND Code = 'MACHINE_DOWN_2H')
                        INSERT INTO AlertRules (Id, TenantId, Code, Name, NameMk, Severity, IsActive, TriggerKind, Threshold, RecipientsJson, DeliveryChannels, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'MACHINE_DOWN_2H', 'Machine down > 2 hours', N'Машина во дефект > 2ч', 2, 1, 3, 2.0, '[""Administrator"",""Manager"",""ProductionLead""]', 'Dashboard', @now, 'P17.E10.5', 0);

                    -- d. Certification expiring < 30 days.
                    IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE TenantId = @tenantId AND Code = 'CERT_EXPIRING_30')
                        INSERT INTO AlertRules (Id, TenantId, Code, Name, NameMk, Severity, IsActive, TriggerKind, Threshold, RecipientsJson, DeliveryChannels, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'CERT_EXPIRING_30', 'Certification expiring < 30 days', N'Сертификат истекува < 30 дена', 2, 1, 4, 30.0, '[""Administrator"",""HRLead""]', 'Dashboard', @now, 'P17.E10.5', 0);

                    -- e. Receipt variance > 5% on single event.
                    IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE TenantId = @tenantId AND Code = 'RECEIPT_VAR_5')
                        INSERT INTO AlertRules (Id, TenantId, Code, Name, NameMk, Severity, IsActive, TriggerKind, Threshold, RecipientsJson, DeliveryChannels, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'RECEIPT_VAR_5', 'Receipt variance > 5%', N'Variance на прием > 5%', 2, 1, 5, 0.05, '[""Administrator"",""Manager"",""WarehouseLead""]', 'Dashboard', @now, 'P17.E10.5', 0);

                    -- f. Subcontractor late on PO milestone (50% planned date).
                    IF NOT EXISTS (SELECT 1 FROM AlertRules WHERE TenantId = @tenantId AND Code = 'SUBCONTRACTOR_LATE')
                        INSERT INTO AlertRules (Id, TenantId, Code, Name, NameMk, Severity, IsActive, TriggerKind, Threshold, RecipientsJson, DeliveryChannels, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @tenantId, 'SUBCONTRACTOR_LATE', 'Subcontractor late on PO milestone', N'Подизведувач задоцнува на milestone', 3, 1, 6, 0.50, '[""Administrator"",""Manager""]', 'Dashboard', @now, 'P17.E10.5', 0);

                    FETCH NEXT FROM tcur INTO @tenantId;
                END
                CLOSE tcur; DEALLOCATE tcur;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertEvents");

            migrationBuilder.DropTable(
                name: "AlertRules");
        }
    }
}
