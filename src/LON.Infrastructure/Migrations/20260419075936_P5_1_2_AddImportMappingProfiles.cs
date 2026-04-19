using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P5_1_2_AddImportMappingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportMappingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetEntity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartnerContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportMappingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportMappingProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingProfiles_TenantId",
                table: "ImportMappingProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingProfiles_TenantId_TargetEntity_PartnerContextId",
                table: "ImportMappingProfiles",
                columns: new[] { "TenantId", "TargetEntity", "PartnerContextId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingProfiles_TenantId_TargetEntity_PartnerContextId_Label",
                table: "ImportMappingProfiles",
                columns: new[] { "TenantId", "TargetEntity", "PartnerContextId", "Label" },
                unique: true,
                filter: "[PartnerContextId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportMappingProfiles");
        }
    }
}
