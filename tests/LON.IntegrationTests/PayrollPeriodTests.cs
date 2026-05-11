using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Finance;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.C3.b — PayrollPeriod / PayrollLine flow. Verifies period creation
/// is idempotent, lines are seeded from attendance, finalize blocks
/// edits, and export stamps ExportedAt.
/// </summary>
public class PayrollPeriodTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public PayrollPeriodTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Create_SeedsLinesPerActiveEmployee_AndIdempotent()
    {
        var client = await AuthedAsync();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        var first = await client.PostAsJsonAsync("/api/Finance/payroll-periods", new
        {
            periodStart = start,
            periodEnd = end,
            standardHoursPerDay = 8m,
            notes = "P16.C3.b test"
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var firstBody = await first.Content.ReadFromJsonAsync<ResultEnvelope<PeriodDto>>();
        firstBody!.Data!.Lines.Count.Should().BeGreaterThan(0,
            "seeder creates one line per active employee");
        var firstId = firstBody.Data.Id;

        // Re-running with same range returns the existing period (idempotent).
        var second = await client.PostAsJsonAsync("/api/Finance/payroll-periods", new
        {
            periodStart = start,
            periodEnd = end,
            standardHoursPerDay = 8m,
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<ResultEnvelope<PeriodDto>>();
        secondBody!.Data!.Id.Should().Be(firstId);
    }

    [Fact]
    public async Task UpdateLine_ThenFinalize_BlocksFurtherEdits()
    {
        var client = await AuthedAsync();
        var start = new DateTime(2026, 2, 1);
        var end = new DateTime(2026, 2, 28);

        var create = await client.PostAsJsonAsync("/api/Finance/payroll-periods", new
        {
            periodStart = start,
            periodEnd = end,
        });
        create.EnsureSuccessStatusCode();
        var period = (await create.Content.ReadFromJsonAsync<ResultEnvelope<PeriodDto>>())!.Data!;
        var firstLine = period.Lines.First();

        var put = await client.PutAsJsonAsync(
            $"/api/Finance/payroll-periods/lines/{firstLine.Id}",
            new
            {
                id = firstLine.Id,
                regularHours = firstLine.RegularHours,
                overtimeHours = firstLine.OvertimeHours,
                absenceHours = firstLine.AbsenceHours,
                bonusAmount = 100m,
                deductionAmount = 20m,
                netAmount = 1234.56m,
                currency = "EUR",
            });
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        var finalize = await client.PostAsync(
            $"/api/Finance/payroll-periods/{period.Id}/finalize", null);
        finalize.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterFinal = (await finalize.Content.ReadFromJsonAsync<ResultEnvelope<PeriodDto>>())!.Data!;
        afterFinal.Status.Should().Be((int)PayrollStatus.Finalized);

        // Editing a finalized line is rejected.
        var rejected = await client.PutAsJsonAsync(
            $"/api/Finance/payroll-periods/lines/{firstLine.Id}",
            new
            {
                id = firstLine.Id,
                regularHours = 0m,
                overtimeHours = 0m,
                absenceHours = 0m,
                bonusAmount = 0m,
                deductionAmount = 0m,
                netAmount = 1m,
                currency = "EUR",
            });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var export = await client.PostAsync(
            $"/api/Finance/payroll-periods/{period.Id}/export", null);
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterExport = (await export.Content.ReadFromJsonAsync<ResultEnvelope<PeriodDto>>())!.Data!;
        afterExport.Status.Should().Be((int)PayrollStatus.Exported);
        afterExport.ExportedAt.Should().NotBeNull();
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record PeriodDto(
        Guid Id, Guid TenantId, DateTime PeriodStart, DateTime PeriodEnd,
        int Status, DateTime? ExportedAt, string? Notes,
        List<LineDto> Lines, DateTime CreatedAt, DateTime? ModifiedAt);
    private sealed record LineDto(
        Guid Id, Guid PeriodId, Guid EmployeeId, string? EmployeeName, string? EmployeeNumber,
        decimal RegularHours, decimal OvertimeHours, decimal AbsenceHours,
        decimal BonusAmount, decimal DeductionAmount, decimal NetAmount, string Currency);
}
