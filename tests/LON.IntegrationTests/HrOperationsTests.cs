using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P10.1 / P10.2 / P10.5 regression guard — HR operations (attendance,
/// absences, operator-machine assignments). Follows Contract Hygiene
/// Protocol §3: POST → GET → DB-level assert.
/// </summary>
public class HrOperationsTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;
    public HrOperationsTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<(Guid EmployeeId, Guid MachineId)> SeedEmployeeAndMachineAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = ctx.Tenants.First();

        var emp = await ctx.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "P10-TEST");
        if (emp is null)
        {
            emp = new Employee
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                EmployeeNumber = "P10-TEST",
                FirstName = "Phase10",
                LastName = "Tester",
                Email = "p10-test@lon.local",
                IsActive = true,
            };
            ctx.Employees.Add(emp);
            await ctx.SaveChangesAsync();
        }

        var machine = await ctx.Machines.FirstOrDefaultAsync(m => m.Code == "P10-MACH");
        if (machine is null)
        {
            var wc = await ctx.WorkCenters.FirstOrDefaultAsync();
            if (wc is null)
            {
                wc = new WorkCenter
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Code = "WC-P10-TEST",
                    Name = "P10 test WC",
                    IsActive = true,
                    Capacity = 1m,
                };
                ctx.WorkCenters.Add(wc);
                await ctx.SaveChangesAsync();
            }

            machine = new Machine
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = "P10-MACH",
                Name = "P10 test machine",
                WorkCenterId = wc.Id,
                IsActive = true,
            };
            ctx.Machines.Add(machine);
            await ctx.SaveChangesAsync();
        }

        return (emp.Id, machine.Id);
    }

    [Fact]
    public async Task ClockIn_Then_ClockOut_ComputesHours()
    {
        var client = await AuthedAsync();
        var (employeeId, _) = await SeedEmployeeAndMachineAsync();

        // Clock-in @ 08:00 UTC today to keep the fixture deterministic.
        var today = DateTime.UtcNow.Date;
        var inAt = today.AddHours(8);
        var outAt = today.AddHours(15).AddMinutes(30);

        // Clear any previous attendance for this employee on this date.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existing = await ctx.AttendanceRecords
                .IgnoreQueryFilters()
                .Where(a => a.EmployeeId == employeeId && a.Date == today)
                .ToListAsync();
            ctx.AttendanceRecords.RemoveRange(existing);
            await ctx.SaveChangesAsync();
        }

        var clockIn = await client.PostAsJsonAsync("/api/Hr/attendance/clock-in",
            new { employeeId, at = inAt });
        clockIn.StatusCode.Should().Be(HttpStatusCode.OK);

        var clockOut = await client.PostAsJsonAsync("/api/Hr/attendance/clock-out",
            new { employeeId, at = outAt });
        clockOut.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = await verify.AttendanceRecords.AsNoTracking()
            .FirstAsync(a => a.EmployeeId == employeeId && a.Date == today);
        record.ClockIn.Should().Be(inAt);
        record.ClockOut.Should().Be(outAt);
        record.Hours.Should().BeApproximately(7.5m, 0.01m);
        record.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task ClockOut_WithoutClockIn_Returns400_WithErrorCode()
    {
        var client = await AuthedAsync();
        var (employeeId, _) = await SeedEmployeeAndMachineAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.UtcNow.Date;
            var existing = await ctx.AttendanceRecords
                .IgnoreQueryFilters()
                .Where(a => a.EmployeeId == employeeId && a.Date == today)
                .ToListAsync();
            ctx.AttendanceRecords.RemoveRange(existing);
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/Hr/attendance/clock-out",
            new { employeeId, at = (DateTime?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<FailureEnvelope>();
        body!.ErrorCode.Should().Be("hr.no_clock_in");
    }

    [Fact]
    public async Task CreateAbsence_ThenApprove_RecordsDecision()
    {
        var client = await AuthedAsync();
        var (employeeId, _) = await SeedEmployeeAndMachineAsync();

        var create = await client.PostAsJsonAsync("/api/Hr/absences",
            new
            {
                employeeId,
                from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                to = new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc),
                type = (int)AbsenceType.Vacation,
                reason = "Annual leave",
            });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var createBody = await create.Content.ReadFromJsonAsync<Envelope<Guid>>();
        var absenceId = createBody!.Data;

        var decide = await client.PostAsJsonAsync($"/api/Hr/absences/{absenceId}/decide",
            new { approve = true });
        decide.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var absence = await ctx.Absences.AsNoTracking().FirstAsync(a => a.Id == absenceId);
        absence.Approved.Should().BeTrue();
        absence.ApprovedAt.Should().NotBeNull();
        absence.ApprovedByUserId.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAbsence_WithBadRange_Returns400_WithErrorCode()
    {
        var client = await AuthedAsync();
        var (employeeId, _) = await SeedEmployeeAndMachineAsync();

        var resp = await client.PostAsJsonAsync("/api/Hr/absences",
            new
            {
                employeeId,
                from = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                to = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                type = (int)AbsenceType.Sick,
                reason = (string?)null,
            });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<FailureEnvelope>();
        body!.ErrorCode.Should().Be("hr.absence_range_invalid");
    }

    [Fact]
    public async Task CreateAssignment_ActiveOnlyFilter_WorksAcrossDateRanges()
    {
        var client = await AuthedAsync();
        var (employeeId, machineId) = await SeedEmployeeAndMachineAsync();

        var now = DateTime.UtcNow;
        var active = await client.PostAsJsonAsync("/api/Hr/assignments",
            new
            {
                employeeId,
                machineId,
                validFrom = now.AddDays(-30),
                validTo = (DateTime?)null,
                notes = "current",
            });
        active.StatusCode.Should().Be(HttpStatusCode.OK);

        var expired = await client.PostAsJsonAsync("/api/Hr/assignments",
            new
            {
                employeeId,
                machineId,
                validFrom = now.AddDays(-90),
                validTo = now.AddDays(-60),
                notes = "expired",
            });
        expired.StatusCode.Should().Be(HttpStatusCode.OK);

        var activeResp = await client.GetFromJsonAsync<Envelope<List<AssignmentRow>>>(
            $"/api/Hr/assignments?employeeId={employeeId}&activeOnly=true");
        activeResp!.Data.Should().OnlyContain(a => a.Notes == "current");

        var allResp = await client.GetFromJsonAsync<Envelope<List<AssignmentRow>>>(
            $"/api/Hr/assignments?employeeId={employeeId}");
        allResp!.Data!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record Envelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);
    private sealed record FailureEnvelope(bool IsSuccess, string? ErrorMessage, string? ErrorCode);
    private sealed record AssignmentRow(
        Guid Id, Guid EmployeeId, string EmployeeNumber, string FullName,
        Guid MachineId, string MachineCode, string MachineName,
        DateTime ValidFrom, DateTime? ValidTo, string? Notes);
}
