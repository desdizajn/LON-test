using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17_E10_AddAiSuggestionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSuggestionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendationCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RecommendationTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StructuredDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserActedOn = table.Column<bool>(type: "bit", nullable: true),
                    UserActedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserActedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSuggestionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSuggestionLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestionLogs_TenantId",
                table: "AiSuggestionLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestionLogs_TenantId_EntityType_EntityId",
                table: "AiSuggestionLogs",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestionLogs_TenantId_GeneratedAt",
                table: "AiSuggestionLogs",
                columns: new[] { "TenantId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestionLogs_TenantId_RecommendationCode_UserActedOn",
                table: "AiSuggestionLogs",
                columns: new[] { "TenantId", "RecommendationCode", "UserActedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSuggestionLogs");
        }
    }
}
