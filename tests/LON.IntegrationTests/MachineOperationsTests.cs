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
/// P11.1–P11.5 regression guard — machine operations (state events, downtime,
/// maintenance schedules, work orders). Follows Contract Hygiene Protocol §3:
/// POST → GET → DB-level assert.
/// </summary>
public class MachineOperationsTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public MachineOperationsTests(LonApiFactory factory) => _factory = factory;

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

    private async Task<Guid> EnsureMachineAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await ctx.Machines.FirstOrDefaultAsync(m => m.Code == "P11-TEST");
        if (existing is not null) return existing.Id;

        var tenant = ctx.Tenants.First();
        var workCenter = ctx.WorkCenters.FirstOrDefault();
        if (workCenter is null)
        {
            workCenter = new WorkCenter
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = "WC-P11-TEST",
                Name = "P11 test work center",
                IsActive = true,
                Capacity = 1m,
            };
            ctx.WorkCenters.Add(workCenter);
            await ctx.SaveChangesAsync();
        }

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = "P11-TEST",
            Name = "P11 Test Machine",
            WorkCenterId = workCenter.Id,
            IsActive = true,
        };
        ctx.Machines.Add(machine);
        await ctx.SaveChangesAsync();
        return machine.Id;
    }

    [Fact]
    public async Task LogState_ThenCurrentStates_ReflectsLatestRow()
    {
        var client = await AuthedAsync();
        var machineId = await EnsureMachineAsync();

        var first = await client.PostAsJsonAsync($"/api/Machines/{machineId}/state-events",
            new { state = (int)MachineState.Idle, changedAt = (DateTime?)null, notes = "initial" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second, later event — current state should resolve to this one.
        await Task.Delay(20);
        var second = await client.PostAsJsonAsync($"/api/Machines/{machineId}/state-events",
            new { state = (int)MachineState.Running, changedAt = (DateTime?)null, notes = "started" });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var resp = await client.GetFromJsonAsync<Envelope<List<CurrentStateRow>>>("/api/Machines/current-states");
        resp!.IsSuccess.Should().BeTrue();
        var row = resp.Data!.FirstOrDefault(r => r.MachineId == machineId);
        row.Should().NotBeNull();
        row!.CurrentState.Should().Be((int)MachineState.Running);
        row.Notes.Should().Be("started");
    }

    [Fact]
    public async Task LogDowntime_ThenClose_ComputesDurationMinutes()
    {
        var client = await AuthedAsync();
        var machineId = await EnsureMachineAsync();

        var start = DateTime.UtcNow.AddMinutes(-30);
        var create = await client.PostAsJsonAsync("/api/Machines/downtime",
            new
            {
                machineId,
                start,
                end = (DateTime?)null,
                category = (int)DowntimeCategory.Breakdown,
                reason = "Motor overheated"
            });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await create.Content.ReadFromJsonAsync<Envelope<Guid>>();
        body!.IsSuccess.Should().BeTrue();
        var id = body.Data;

        var closeAt = start.AddMinutes(18);
        var close = await client.PostAsJsonAsync($"/api/Machines/downtime/{id}/close", new { end = closeAt });
        close.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = await ctx.DowntimeEvents.AsNoTracking().FirstAsync(e => e.Id == id);
        evt.End.Should().NotBeNull();
        evt.DurationMinutes.Should().BeApproximately(18m, 0.01m);
    }

    [Fact]
    public async Task DowntimeWithBadReason_Returns400_WithErrorCode()
    {
        var client = await AuthedAsync();
        var machineId = await EnsureMachineAsync();

        var resp = await client.PostAsJsonAsync("/api/Machines/downtime",
            new
            {
                machineId,
                start = DateTime.UtcNow,
                end = (DateTime?)null,
                category = (int)DowntimeCategory.Other,
                reason = "   "
            });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<FailureEnvelope>();
        body!.ErrorCode.Should().Be("downtime.reason_required");
    }

    [Fact]
    public async Task CompleteWorkOrder_RollsSchedulesNextDueForward()
    {
        var client = await AuthedAsync();
        var machineId = await EnsureMachineAsync();

        // Create a schedule with LastDone=2026-01-01, NextDue=2026-03-01.
        var create = await client.PostAsJsonAsync("/api/Machines/maintenance-schedules",
            new
            {
                machineId,
                taskDescription = "Monthly motor inspection",
                intervalDays = 30,
                lastDone = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                nextDue = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheduleId = (await create.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;

        // Create a work order linked to that schedule.
        var wo = await client.PostAsJsonAsync("/api/Machines/maintenance-work-orders",
            new
            {
                machineId,
                scheduleId,
                scheduledDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                taskDescription = "March monthly inspection",
            });
        wo.StatusCode.Should().Be(HttpStatusCode.OK);
        var woId = (await wo.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;

        // Complete it on 2026-03-05 → NextDue should jump to 2026-04-04 (30 days).
        var completedAt = new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);
        var done = await client.PostAsJsonAsync(
            $"/api/Machines/maintenance-work-orders/{woId}/complete",
            new { completedAt, notes = "All OK", costImpact = 250m });
        done.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schedule = await ctx.MaintenanceSchedules.AsNoTracking().FirstAsync(s => s.Id == scheduleId);
        schedule.LastDone!.Value.Date.Should().Be(new DateTime(2026, 3, 5));
        schedule.NextDue.Date.Should().Be(new DateTime(2026, 4, 4));

        var completedWo = await ctx.MaintenanceWorkOrders.AsNoTracking().FirstAsync(w => w.Id == woId);
        completedWo.CompletedAt.Should().NotBeNull();
        completedWo.CostImpact.Should().Be(250m);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record Envelope<T>(bool IsSuccess, T? Data, string? ErrorMessage, string? ErrorCode);
    private sealed record FailureEnvelope(bool IsSuccess, string? ErrorMessage, string? ErrorCode);

    private sealed record CurrentStateRow(
        Guid MachineId,
        string MachineCode,
        string MachineName,
        Guid WorkCenterId,
        string WorkCenterCode,
        int? CurrentState,
        DateTime? Since,
        string? Notes);
}
