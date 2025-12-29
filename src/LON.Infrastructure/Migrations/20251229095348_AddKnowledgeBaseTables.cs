using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HSCode",
                table: "CustomsDeclarationLines",
                newName: "TariffCode");

            migrationBuilder.AddColumn<string>(
                name: "ActiveTransportIdentification",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfDestination",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfDispatch",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclarationType",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTerms",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "CustomsDeclarations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasContainer",
                table: "CustomsDeclarations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LONAuthorizationId",
                table: "CustomsDeclarations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationOfGoods",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageDescription",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousProcedureCode",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcedureCode",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverAddress",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverName",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverTaxId",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderAddress",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderCountry",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialRemarks",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalInvoiceAmount",
                table: "CustomsDeclarations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalPackages",
                table: "CustomsDeclarations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportIdentification",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportMode",
                table: "CustomsDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentRate",
                table: "CustomsDeclarationLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationMethod",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossWeight",
                table: "CustomsDeclarationLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ItemPrice",
                table: "CustomsDeclarationLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NationalSuffix",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetWeight",
                table: "CustomsDeclarationLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfPackages",
                table: "CustomsDeclarationLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageMarks",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousMRN",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StatisticalValue",
                table: "CustomsDeclarationLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TARICSuffix",
                table: "CustomsDeclarationLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UsedQuantityFromPrevious",
                table: "CustomsDeclarationLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DescriptionMK = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DescriptionEN = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BoxNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Tooltip = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeListItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeclarationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValidationLogic = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ErrorMessageMK = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ErrorMessageEN = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReferenceDocument = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcedureCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeclarationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LONAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorizationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EconomicConditionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GuaranteeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GuaranteeReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompetentCustomsOffice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupervisoryOffice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletionPeriodDays = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LONAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LONAuthorizations_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TariffCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TariffNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TARBR = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TAROZ1 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    TAROZ2 = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    TAROZ3 = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CustomsRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    UnitMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    FI = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FU = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PV = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LONAuthorizationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LONAuthorizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportTariffCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompensatingProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompensatingTariffCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    YieldRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllowedWastePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LONAuthorizationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LONAuthorizationItems_Items_CompensatingProductId",
                        column: x => x.CompensatingProductId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LONAuthorizationItems_Items_ImportItemId",
                        column: x => x.ImportItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LONAuthorizationItems_LONAuthorizations_LONAuthorizationId",
                        column: x => x.LONAuthorizationId,
                        principalTable: "LONAuthorizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomsRegulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CelexNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OfficialGazetteRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TariffNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DescriptionEN = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescriptionMK = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LegalBasis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TariffCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsRegulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomsRegulations_TariffCodes_TariffCodeId",
                        column: x => x.TariffCodeId,
                        principalTable: "TariffCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_LONAuthorizationId",
                table: "CustomsDeclarations",
                column: "LONAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeListItems_BoxNumber",
                table: "CodeListItems",
                column: "BoxNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CodeListItems_IsActive",
                table: "CodeListItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CodeListItems_ListType_Code",
                table: "CodeListItems",
                columns: new[] { "ListType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsRegulations_CelexNumber",
                table: "CustomsRegulations",
                column: "CelexNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsRegulations_TariffCodeId",
                table: "CustomsRegulations",
                column: "TariffCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeclarationRules_FieldName_ProcedureCode",
                table: "DeclarationRules",
                columns: new[] { "FieldName", "ProcedureCode" });

            migrationBuilder.CreateIndex(
                name: "IX_DeclarationRules_RuleCode",
                table: "DeclarationRules",
                column: "RuleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizationItems_CompensatingProductId",
                table: "LONAuthorizationItems",
                column: "CompensatingProductId");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizationItems_ImportItemId",
                table: "LONAuthorizationItems",
                column: "ImportItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizationItems_LONAuthorizationId",
                table: "LONAuthorizationItems",
                column: "LONAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_AuthorizationNumber",
                table: "LONAuthorizations",
                column: "AuthorizationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_PartnerId",
                table: "LONAuthorizations",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LONAuthorizations_Status",
                table: "LONAuthorizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TariffCodes_TariffNumber",
                table: "TariffCodes",
                column: "TariffNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomsDeclarations_LONAuthorizations_LONAuthorizationId",
                table: "CustomsDeclarations",
                column: "LONAuthorizationId",
                principalTable: "LONAuthorizations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomsDeclarations_LONAuthorizations_LONAuthorizationId",
                table: "CustomsDeclarations");

            migrationBuilder.DropTable(
                name: "CodeListItems");

            migrationBuilder.DropTable(
                name: "CustomsRegulations");

            migrationBuilder.DropTable(
                name: "DeclarationRules");

            migrationBuilder.DropTable(
                name: "LONAuthorizationItems");

            migrationBuilder.DropTable(
                name: "TariffCodes");

            migrationBuilder.DropTable(
                name: "LONAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_CustomsDeclarations_LONAuthorizationId",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ActiveTransportIdentification",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "CountryOfDestination",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "CountryOfDispatch",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "DeclarationType",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "DeliveryTerms",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "HasContainer",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "LONAuthorizationId",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "LocationOfGoods",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "PackageDescription",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "PreviousProcedureCode",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ProcedureCode",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ReceiverAddress",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ReceiverName",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "ReceiverTaxId",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "SenderAddress",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "SenderCountry",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "SpecialRemarks",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "TotalInvoiceAmount",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "TotalPackages",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "TransportIdentification",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "TransportMode",
                table: "CustomsDeclarations");

            migrationBuilder.DropColumn(
                name: "AdjustmentRate",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "CalculationMethod",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "GrossWeight",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "ItemPrice",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "NationalSuffix",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "NetWeight",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "NumberOfPackages",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "PackageMarks",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "PreviousMRN",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "StatisticalValue",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "TARICSuffix",
                table: "CustomsDeclarationLines");

            migrationBuilder.DropColumn(
                name: "UsedQuantityFromPrevious",
                table: "CustomsDeclarationLines");

            migrationBuilder.RenameColumn(
                name: "TariffCode",
                table: "CustomsDeclarationLines",
                newName: "HSCode");
        }
    }
}
