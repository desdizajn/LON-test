using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P6_32_FilteredUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_TenantId_Code",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_TenantId_Code",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_TariffCodes_TariffNumber",
                table: "TariffCodes");

            migrationBuilder.DropIndex(
                name: "IX_Routings_ItemId_Version",
                table: "Routings");

            migrationBuilder.DropIndex(
                name: "IX_RoutingOperations_RoutingId_SequenceNumber",
                table: "RoutingOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionReceipts_TenantId_ReceiptNumber",
                table: "ProductionReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_TenantId_OrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderOperations_ProductionOrderId_SequenceNumber",
                table: "ProductionOrderOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_ProductionOrderId_LineNumber",
                table: "ProductionOrderMaterials");

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
                name: "IX_Locations_WarehouseId_Code",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_ItemUoMConversions_ItemId_FromUoMId_ToUoMId",
                table: "ItemUoMConversions");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantId_Code",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ImportMappingProfiles_TenantId_TargetEntity_PartnerContextId_Label",
                table: "ImportMappingProfiles");

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
                name: "IX_DeclarationRules_RuleCode",
                table: "DeclarationRules");

            migrationBuilder.DropIndex(
                name: "IX_CustomsProcedures_Code",
                table: "CustomsProcedures");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId_DeclarationNumber",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId_MRN",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarationLines_CustomsDeclarationId_LineNumber",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropIndex(
                name: "IX_CodeListItems_ListType_Code",
                table: "CodeListItems");

            migrationBuilder.DropIndex(
                name: "IX_BOMs_ItemId_Version",
                table: "BOMs");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_BOMId_LineNumber",
                table: "BOMLines");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_TenantId_Code",
                table: "WorkCenters",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_Code",
                table: "Warehouses",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TariffCodes_TariffNumber",
                table: "TariffCodes",
                column: "TariffNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Routings_ItemId_Version",
                table: "Routings",
                columns: new[] { "ItemId", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingOperations_RoutingId_SequenceNumber",
                table: "RoutingOperations",
                columns: new[] { "RoutingId", "SequenceNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceipts_TenantId_ReceiptNumber",
                table: "ProductionReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_OrderNumber",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderOperations_ProductionOrderId_SequenceNumber",
                table: "ProductionOrderOperations",
                columns: new[] { "ProductionOrderId", "SequenceNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_ProductionOrderId_LineNumber",
                table: "ProductionOrderMaterials",
                columns: new[] { "ProductionOrderId", "LineNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_TenantId_Code",
                table: "Partners",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MRNRegistries_TenantId_MRN",
                table: "MRNRegistries",
                columns: new[] { "TenantId", "MRN" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_TenantId_IssueNumber",
                table: "MaterialIssues",
                columns: new[] { "TenantId", "IssueNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_TenantId_Code",
                table: "Machines",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_TenantId_AuthorizationNumber",
                table: "LONAuthorizations",
                columns: new[] { "TenantId", "AuthorizationNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_WarehouseId_Code",
                table: "Locations",
                columns: new[] { "WarehouseId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUoMConversions_ItemId_FromUoMId_ToUoMId",
                table: "ItemUoMConversions",
                columns: new[] { "ItemId", "FromUoMId", "ToUoMId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantId_Code",
                table: "Items",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingProfiles_TenantId_TargetEntity_PartnerContextId_Label",
                table: "ImportMappingProfiles",
                columns: new[] { "TenantId", "TargetEntity", "PartnerContextId", "Label" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeAccounts_TenantId_AccountNumber",
                table: "GuaranteeAccounts",
                columns: new[] { "TenantId", "AccountNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_Email",
                table: "Employees",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_EmployeeNumber",
                table: "Employees",
                columns: new[] { "TenantId", "EmployeeNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DeclarationRules_RuleCode",
                table: "DeclarationRules",
                column: "RuleCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsProcedures_Code",
                table: "CustomsProcedures",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_TenantId_DeclarationNumber",
                table: "CustomsDeclarations",
                columns: new[] { "TenantId", "DeclarationNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_TenantId_MRN",
                table: "CustomsDeclarations",
                columns: new[] { "TenantId", "MRN" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarationLines_CustomsDeclarationId_LineNumber",
                table: "CustomsDeclarationLines",
                columns: new[] { "CustomsDeclarationId", "LineNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CodeListItems_ListType_Code",
                table: "CodeListItems",
                columns: new[] { "ListType", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BOMs_ItemId_Version",
                table: "BOMs",
                columns: new[] { "ItemId", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_BOMId_LineNumber",
                table: "BOMLines",
                columns: new[] { "BOMId", "LineNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
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
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_TariffCodes_TariffNumber",
                table: "TariffCodes");

            migrationBuilder.DropIndex(
                name: "IX_Routings_ItemId_Version",
                table: "Routings");

            migrationBuilder.DropIndex(
                name: "IX_RoutingOperations_RoutingId_SequenceNumber",
                table: "RoutingOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionReceipts_TenantId_ReceiptNumber",
                table: "ProductionReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_TenantId_OrderNumber",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderOperations_ProductionOrderId_SequenceNumber",
                table: "ProductionOrderOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderMaterials_ProductionOrderId_LineNumber",
                table: "ProductionOrderMaterials");

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
                name: "IX_Locations_WarehouseId_Code",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_ItemUoMConversions_ItemId_FromUoMId_ToUoMId",
                table: "ItemUoMConversions");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantId_Code",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ImportMappingProfiles_TenantId_TargetEntity_PartnerContextId_Label",
                table: "ImportMappingProfiles");

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
                name: "IX_DeclarationRules_RuleCode",
                table: "DeclarationRules");

            migrationBuilder.DropIndex(
                name: "IX_CustomsProcedures_Code",
                table: "CustomsProcedures");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId_DeclarationNumber",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_TenantId_MRN",
                table: "CustomsDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarationLines_CustomsDeclarationId_LineNumber",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropIndex(
                name: "IX_CodeListItems_ListType_Code",
                table: "CodeListItems");

            migrationBuilder.DropIndex(
                name: "IX_BOMs_ItemId_Version",
                table: "BOMs");

            migrationBuilder.DropIndex(
                name: "IX_BOMLines_BOMId_LineNumber",
                table: "BOMLines");

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
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TariffCodes_TariffNumber",
                table: "TariffCodes",
                column: "TariffNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routings_ItemId_Version",
                table: "Routings",
                columns: new[] { "ItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingOperations_RoutingId_SequenceNumber",
                table: "RoutingOperations",
                columns: new[] { "RoutingId", "SequenceNumber" },
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
                name: "IX_ProductionOrderOperations_ProductionOrderId_SequenceNumber",
                table: "ProductionOrderOperations",
                columns: new[] { "ProductionOrderId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderMaterials_ProductionOrderId_LineNumber",
                table: "ProductionOrderMaterials",
                columns: new[] { "ProductionOrderId", "LineNumber" },
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
                name: "IX_Locations_WarehouseId_Code",
                table: "Locations",
                columns: new[] { "WarehouseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemUoMConversions_ItemId_FromUoMId_ToUoMId",
                table: "ItemUoMConversions",
                columns: new[] { "ItemId", "FromUoMId", "ToUoMId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantId_Code",
                table: "Items",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingProfiles_TenantId_TargetEntity_PartnerContextId_Label",
                table: "ImportMappingProfiles",
                columns: new[] { "TenantId", "TargetEntity", "PartnerContextId", "Label" },
                unique: true,
                filter: "[PartnerContextId] IS NOT NULL");

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
                name: "IX_DeclarationRules_RuleCode",
                table: "DeclarationRules",
                column: "RuleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsProcedures_Code",
                table: "CustomsProcedures",
                column: "Code",
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

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarationLines_CustomsDeclarationId_LineNumber",
                table: "CustomsDeclarationLines",
                columns: new[] { "CustomsDeclarationId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeListItems_ListType_Code",
                table: "CodeListItems",
                columns: new[] { "ListType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOMs_ItemId_Version",
                table: "BOMs",
                columns: new[] { "ItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOMLines_BOMId_LineNumber",
                table: "BOMLines",
                columns: new[] { "BOMId", "LineNumber" },
                unique: true);
        }
    }
}
