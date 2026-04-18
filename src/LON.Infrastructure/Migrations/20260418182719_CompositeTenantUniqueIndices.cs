using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompositeTenantUniqueIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_Code",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_TransferNumber",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_Code",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_ReceiptNumber",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionReceipts_ReceiptNumber",
                table: "ProductionReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_OrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_PickTasks_TaskNumber",
                table: "PickTasks");

            migrationBuilder.DropIndex(
                name: "IX_PickingWaves_WaveNumber",
                table: "PickingWaves");

            migrationBuilder.DropIndex(
                name: "IX_Partners_Code",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_MRNRegistries_MRN",
                table: "MRNRegistries");

            migrationBuilder.DropIndex(
                name: "IX_MaterialIssues_IssueNumber",
                table: "MaterialIssues");

            migrationBuilder.DropIndex(
                name: "IX_Machines_Code",
                table: "Machines");

            migrationBuilder.DropIndex(
                name: "IX_LONAuthorizations_AuthorizationNumber",
                table: "LONAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_Items_Code",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_GuaranteeAccounts_AccountNumber",
                table: "GuaranteeAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Employees_Email",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_CycleCounts_CountNumber",
                table: "CycleCounts");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_DeclarationNumber",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_MRN",
                table: "CustomsDeclarations");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_TenantId_Code",
                table: "WorkCenters",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_Code",
                table: "Warehouses",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TenantId_TransferNumber",
                table: "Transfers",
                columns: new[] { "TenantId", "TransferNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId_ShipmentNumber",
                table: "Shipments",
                columns: new[] { "TenantId", "ShipmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId_Code",
                table: "Shifts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TenantId_ReceiptNumber",
                table: "Receipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceipts_TenantId_ReceiptNumber",
                table: "ProductionReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_OrderNumber",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickTasks_TenantId_TaskNumber",
                table: "PickTasks",
                columns: new[] { "TenantId", "TaskNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickingWaves_TenantId_WaveNumber",
                table: "PickingWaves",
                columns: new[] { "TenantId", "WaveNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_TenantId_Code",
                table: "Partners",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MRNRegistries_TenantId_MRN",
                table: "MRNRegistries",
                columns: new[] { "TenantId", "MRN" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_TenantId_IssueNumber",
                table: "MaterialIssues",
                columns: new[] { "TenantId", "IssueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_TenantId_Code",
                table: "Machines",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_TenantId_AuthorizationNumber",
                table: "LONAuthorizations",
                columns: new[] { "TenantId", "AuthorizationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantId_Code",
                table: "Items",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeAccounts_TenantId_AccountNumber",
                table: "GuaranteeAccounts",
                columns: new[] { "TenantId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_Email",
                table: "Employees",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_EmployeeNumber",
                table: "Employees",
                columns: new[] { "TenantId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CycleCounts_TenantId_CountNumber",
                table: "CycleCounts",
                columns: new[] { "TenantId", "CountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_TenantId_DeclarationNumber",
                table: "CustomsDeclarations",
                columns: new[] { "TenantId", "DeclarationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_TenantId_MRN",
                table: "CustomsDeclarations",
                columns: new[] { "TenantId", "MRN" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_TenantId_Code",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_TenantId_Code",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_TenantId_TransferNumber",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_TenantId_ShipmentNumber",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_TenantId_Code",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_TenantId_ReceiptNumber",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionReceipts_TenantId_ReceiptNumber",
                table: "ProductionReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_TenantId_OrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_PickTasks_TenantId_TaskNumber",
                table: "PickTasks");

            migrationBuilder.DropIndex(
                name: "IX_PickingWaves_TenantId_WaveNumber",
                table: "PickingWaves");

            migrationBuilder.DropIndex(
                name: "IX_Partners_TenantId_Code",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_MRNRegistries_TenantId_MRN",
                table: "MRNRegistries");

            migrationBuilder.DropIndex(
                name: "IX_MaterialIssues_TenantId_IssueNumber",
                table: "MaterialIssues");

            migrationBuilder.DropIndex(
                name: "IX_Machines_TenantId_Code",
                table: "Machines");

            migrationBuilder.DropIndex(
                name: "IX_LONAuthorizations_TenantId_AuthorizationNumber",
                table: "LONAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantId_Code",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_GuaranteeAccounts_TenantId_AccountNumber",
                table: "GuaranteeAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_Email",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_CycleCounts_TenantId_CountNumber",
                table: "CycleCounts");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId_DeclarationNumber",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId_MRN",
                table: "CustomsDeclarations");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_Code",
                table: "WorkCenters",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TransferNumber",
                table: "Transfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments",
                column: "ShipmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Code",
                table: "Shifts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ReceiptNumber",
                table: "Receipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceipts_ReceiptNumber",
                table: "ProductionReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_OrderNumber",
                table: "ProductionOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickTasks_TaskNumber",
                table: "PickTasks",
                column: "TaskNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickingWaves_WaveNumber",
                table: "PickingWaves",
                column: "WaveNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_Code",
                table: "Partners",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MRNRegistries_MRN",
                table: "MRNRegistries",
                column: "MRN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_IssueNumber",
                table: "MaterialIssues",
                column: "IssueNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Code",
                table: "Machines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_AuthorizationNumber",
                table: "LONAuthorizations",
                column: "AuthorizationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_Code",
                table: "Items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeAccounts_AccountNumber",
                table: "GuaranteeAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CycleCounts_CountNumber",
                table: "CycleCounts",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_DeclarationNumber",
                table: "CustomsDeclarations",
                column: "DeclarationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_MRN",
                table: "CustomsDeclarations",
                column: "MRN",
                unique: true);
        }
    }
}
