using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P16_C2_AddEmployeeCertification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCertifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificationName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SkillArea = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCertifications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCertifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_EmployeeId",
                table: "EmployeeCertifications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_ExpiryDate",
                table: "EmployeeCertifications",
                column: "ExpiryDate",
                filter: "[IsDeleted] = 0 AND [ExpiryDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_TenantId",
                table: "EmployeeCertifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_TenantId_EmployeeId",
                table: "EmployeeCertifications",
                columns: new[] { "TenantId", "EmployeeId" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCertifications");
        }
    }
}
