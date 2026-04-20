using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LON.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P11_MachineOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DowntimeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReportedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DowntimeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DowntimeEvents_Employees_ReportedByEmployeeId",
                        column: x => x.ReportedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DowntimeEvents_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DowntimeEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineStateEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineStateEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineStateEvents_Employees_ChangedByEmployeeId",
                        column: x => x.ChangedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineStateEvents_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineStateEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    LastDone = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextDue = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceWorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicianEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Employees_TechnicianEmployeeId",
                        column: x => x.TechnicianEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_MaintenanceSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_MachineId",
                table: "DowntimeEvents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_ReportedByEmployeeId",
                table: "DowntimeEvents",
                column: "ReportedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_TenantId",
                table: "DowntimeEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_TenantId_End",
                table: "DowntimeEvents",
                columns: new[] { "TenantId", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_TenantId_MachineId_Start",
                table: "DowntimeEvents",
                columns: new[] { "TenantId", "MachineId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_ChangedByEmployeeId",
                table: "MachineStateEvents",
                column: "ChangedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_MachineId",
                table: "MachineStateEvents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_TenantId",
                table: "MachineStateEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_TenantId_MachineId_ChangedAt",
                table: "MachineStateEvents",
                columns: new[] { "TenantId", "MachineId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_MachineId",
                table: "MaintenanceSchedules",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_TenantId",
                table: "MaintenanceSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_TenantId_MachineId",
                table: "MaintenanceSchedules",
                columns: new[] { "TenantId", "MachineId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_TenantId_NextDue",
                table: "MaintenanceSchedules",
                columns: new[] { "TenantId", "NextDue" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_MachineId",
                table: "MaintenanceWorkOrders",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_ScheduleId",
                table: "MaintenanceWorkOrders",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_TechnicianEmployeeId",
                table: "MaintenanceWorkOrders",
                column: "TechnicianEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_TenantId",
                table: "MaintenanceWorkOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_TenantId_CompletedAt",
                table: "MaintenanceWorkOrders",
                columns: new[] { "TenantId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_TenantId_MachineId_ScheduledDate",
                table: "MaintenanceWorkOrders",
                columns: new[] { "TenantId", "MachineId", "ScheduledDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DowntimeEvents");

            migrationBuilder.DropTable(
                name: "MachineStateEvents");

            migrationBuilder.DropTable(
                name: "MaintenanceWorkOrders");

            migrationBuilder.DropTable(
                name: "MaintenanceSchedules");
        }
    }
}
