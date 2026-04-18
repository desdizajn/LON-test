using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToRemainingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WorkCenters",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Transfers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TransferLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TraceLinks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ShipmentLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Shifts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Routings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RoutingOperations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductionReceipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductionOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductionOrderOperations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductionOrderMaterials",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PickTasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PickingWaves",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MRNRegistries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MaterialIssues",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Machines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LONAuthorizations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LONAuthorizationItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GuaranteeLedgerEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GuaranteeAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DutyCalculations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CycleCounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CycleCountLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CustomsDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CustomsDeclarations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CustomsDeclarationLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BOMs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BOMLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BatchGenealogies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_TenantId",
                table: "WorkCenters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TenantId",
                table: "Transfers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLines_TenantId",
                table: "TransferLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceLinks_TenantId",
                table: "TraceLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId",
                table: "Shipments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_TenantId",
                table: "ShipmentLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId",
                table: "Shifts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Routings_TenantId",
                table: "Routings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingOperations_TenantId",
                table: "RoutingOperations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceipts_TenantId",
                table: "ProductionReceipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId",
                table: "ProductionOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderOperations_TenantId",
                table: "ProductionOrderOperations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_TenantId",
                table: "ProductionOrderMaterials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PickTasks_TenantId",
                table: "PickTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingWaves_TenantId",
                table: "PickingWaves",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MRNRegistries_TenantId",
                table: "MRNRegistries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_TenantId",
                table: "MaterialIssues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_TenantId",
                table: "Machines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_TenantId",
                table: "LONAuthorizations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizationItems_TenantId",
                table: "LONAuthorizationItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeLedgerEntries_TenantId",
                table: "GuaranteeLedgerEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeAccounts_TenantId",
                table: "GuaranteeAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyCalculations_TenantId",
                table: "DutyCalculations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCounts_TenantId",
                table: "CycleCounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCountLines_TenantId",
                table: "CycleCountLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDocuments_TenantId",
                table: "CustomsDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_TenantId",
                table: "CustomsDeclarations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarationLines_TenantId",
                table: "CustomsDeclarationLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BOMs_TenantId",
                table: "BOMs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_TenantId",
                table: "BOMLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchGenealogies_TenantId",
                table: "BatchGenealogies",
                column: "TenantId");

            // Backfill TenantId for every row in the 31 newly-scoped tables so
            // the FK constraints added next accept them. TEKSPORT (seeded in P1.1)
            // is the default. Fallback to the first active tenant for DBs that
            // for some reason never had TEKSPORT seeded.
            migrationBuilder.Sql(@"
DECLARE @tenantId UNIQUEIDENTIFIER =
    (SELECT TOP 1 Id FROM Tenants WHERE Code = 'TEKSPORT');
IF @tenantId IS NULL
    SET @tenantId = (SELECT TOP 1 Id FROM Tenants WHERE IsActive = 1 ORDER BY CreatedAt);

IF @tenantId IS NOT NULL
BEGIN
    UPDATE BatchGenealogies            SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE BOMLines                    SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE BOMs                        SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE CustomsDeclarationLines     SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE CustomsDeclarations         SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE CustomsDocuments            SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE CycleCountLines             SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE CycleCounts                 SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE DutyCalculations            SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE GuaranteeAccounts           SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE GuaranteeLedgerEntries      SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE LONAuthorizationItems       SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE LONAuthorizations           SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Machines                    SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE MaterialIssues              SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE MRNRegistries               SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE PickingWaves                SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE PickTasks                   SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE ProductionOrderMaterials    SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE ProductionOrderOperations   SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE ProductionOrders            SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE ProductionReceipts          SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE RoutingOperations           SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Routings                    SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Shifts                      SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE ShipmentLines               SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Shipments                   SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE TraceLinks                  SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE TransferLines               SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE Transfers                   SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
    UPDATE WorkCenters                 SET TenantId = @tenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
END
");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchGenealogies_Tenants_TenantId",
                table: "BatchGenealogies",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BOMLines_Tenants_TenantId",
                table: "BOMLines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BOMs_Tenants_TenantId",
                table: "BOMs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomsDeclarationLines_Tenants_TenantId",
                table: "CustomsDeclarationLines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomsDeclarations_Tenants_TenantId",
                table: "CustomsDeclarations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomsDocuments_Tenants_TenantId",
                table: "CustomsDocuments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CycleCountLines_Tenants_TenantId",
                table: "CycleCountLines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CycleCounts_Tenants_TenantId",
                table: "CycleCounts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DutyCalculations_Tenants_TenantId",
                table: "DutyCalculations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuaranteeAccounts_Tenants_TenantId",
                table: "GuaranteeAccounts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuaranteeLedgerEntries_Tenants_TenantId",
                table: "GuaranteeLedgerEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LONAuthorizationItems_Tenants_TenantId",
                table: "LONAuthorizationItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LONAuthorizations_Tenants_TenantId",
                table: "LONAuthorizations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_Tenants_TenantId",
                table: "Machines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialIssues_Tenants_TenantId",
                table: "MaterialIssues",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MRNRegistries_Tenants_TenantId",
                table: "MRNRegistries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PickingWaves_Tenants_TenantId",
                table: "PickingWaves",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PickTasks_Tenants_TenantId",
                table: "PickTasks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderMaterials_Tenants_TenantId",
                table: "ProductionOrderMaterials",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderOperations_Tenants_TenantId",
                table: "ProductionOrderOperations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Tenants_TenantId",
                table: "ProductionOrders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionReceipts_Tenants_TenantId",
                table: "ProductionReceipts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoutingOperations_Tenants_TenantId",
                table: "RoutingOperations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Routings_Tenants_TenantId",
                table: "Routings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Tenants_TenantId",
                table: "Shifts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentLines_Tenants_TenantId",
                table: "ShipmentLines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_Tenants_TenantId",
                table: "Shipments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TraceLinks_Tenants_TenantId",
                table: "TraceLinks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferLines_Tenants_TenantId",
                table: "TransferLines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Tenants_TenantId",
                table: "Transfers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkCenters_Tenants_TenantId",
                table: "WorkCenters",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BatchGenealogies_Tenants_TenantId",
                table: "BatchGenealogies");

            migrationBuilder.DropForeignKey(
                name: "FK_BOMLines_Tenants_TenantId",
                table: "BOMLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BOMs_Tenants_TenantId",
                table: "BOMs");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomsDeclarationLines_Tenants_TenantId",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomsDeclarations_Tenants_TenantId",
                table: "CustomsDeclarations");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomsDocuments_Tenants_TenantId",
                table: "CustomsDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_CycleCountLines_Tenants_TenantId",
                table: "CycleCountLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CycleCounts_Tenants_TenantId",
                table: "CycleCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyCalculations_Tenants_TenantId",
                table: "DutyCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_GuaranteeAccounts_Tenants_TenantId",
                table: "GuaranteeAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_GuaranteeLedgerEntries_Tenants_TenantId",
                table: "GuaranteeLedgerEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LONAuthorizationItems_Tenants_TenantId",
                table: "LONAuthorizationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_LONAuthorizations_Tenants_TenantId",
                table: "LONAuthorizations");

            migrationBuilder.DropForeignKey(
                name: "FK_Machines_Tenants_TenantId",
                table: "Machines");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialIssues_Tenants_TenantId",
                table: "MaterialIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_MRNRegistries_Tenants_TenantId",
                table: "MRNRegistries");

            migrationBuilder.DropForeignKey(
                name: "FK_PickingWaves_Tenants_TenantId",
                table: "PickingWaves");

            migrationBuilder.DropForeignKey(
                name: "FK_PickTasks_Tenants_TenantId",
                table: "PickTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderMaterials_Tenants_TenantId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderOperations_Tenants_TenantId",
                table: "ProductionOrderOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Tenants_TenantId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionReceipts_Tenants_TenantId",
                table: "ProductionReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_RoutingOperations_Tenants_TenantId",
                table: "RoutingOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_Routings_Tenants_TenantId",
                table: "Routings");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Tenants_TenantId",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentLines_Tenants_TenantId",
                table: "ShipmentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_Tenants_TenantId",
                table: "Shipments");

            migrationBuilder.DropForeignKey(
                name: "FK_TraceLinks_Tenants_TenantId",
                table: "TraceLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferLines_Tenants_TenantId",
                table: "TransferLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_Tenants_TenantId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkCenters_Tenants_TenantId",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_TenantId",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_TenantId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_TransferLines_TenantId",
                table: "TransferLines");

            migrationBuilder.DropIndex(
                name: "IX_TraceLinks_TenantId",
                table: "TraceLinks");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_TenantId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentLines_TenantId",
                table: "ShipmentLines");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_TenantId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Routings_TenantId",
                table: "Routings");

            migrationBuilder.DropIndex(
                name: "IX_RoutingOperations_TenantId",
                table: "RoutingOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionReceipts_TenantId",
                table: "ProductionReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_TenantId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderOperations_TenantId",
                table: "ProductionOrderOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_TenantId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropIndex(
                name: "IX_PickTasks_TenantId",
                table: "PickTasks");

            migrationBuilder.DropIndex(
                name: "IX_PickingWaves_TenantId",
                table: "PickingWaves");

            migrationBuilder.DropIndex(
                name: "IX_MRNRegistries_TenantId",
                table: "MRNRegistries");

            migrationBuilder.DropIndex(
                name: "IX_MaterialIssues_TenantId",
                table: "MaterialIssues");

            migrationBuilder.DropIndex(
                name: "IX_Machines_TenantId",
                table: "Machines");

            migrationBuilder.DropIndex(
                name: "IX_LONAuthorizations_TenantId",
                table: "LONAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_LONAuthorizationItems_TenantId",
                table: "LONAuthorizationItems");

            migrationBuilder.DropIndex(
                name: "IX_GuaranteeLedgerEntries_TenantId",
                table: "GuaranteeLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_GuaranteeAccounts_TenantId",
                table: "GuaranteeAccounts");

            migrationBuilder.DropIndex(
                name: "IX_DutyCalculations_TenantId",
                table: "DutyCalculations");

            migrationBuilder.DropIndex(
                name: "IX_CycleCounts_TenantId",
                table: "CycleCounts");

            migrationBuilder.DropIndex(
                name: "IX_CycleCountLines_TenantId",
                table: "CycleCountLines");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDocuments_TenantId",
                table: "CustomsDocuments");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarationLines_TenantId",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropIndex(
                name: "IX_BOMs_TenantId",
                table: "BOMs");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_TenantId",
                table: "BOMLines");

            migrationBuilder.DropIndex(
                name: "IX_BatchGenealogies_TenantId",
                table: "BatchGenealogies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TransferLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TraceLinks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ShipmentLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Routings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RoutingOperations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductionReceipts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductionOrderOperations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductionOrderMaterials");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PickTasks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PickingWaves");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MRNRegistries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MaterialIssues");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LONAuthorizations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LONAuthorizationItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GuaranteeLedgerEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GuaranteeAccounts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DutyCalculations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CycleCounts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CycleCountLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomsDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BOMs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BOMLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BatchGenealogies");
        }
    }
}
