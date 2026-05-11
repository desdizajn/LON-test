using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P16.C2 — EmployeeCertification E2E flow. CRUD + tenant isolation +
/// expiring-within-N-days filter logic.
/// </summary>
public class EmployeeCertificationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public EmployeeCertificationTests(LonApiFactory factory) => _factory = factory;

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

    private async Task<Guid> SeedEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Code == "TEKSPORT");

        var emp = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "P16C2-T-1");
        if (emp is not null) return emp.Id;

        emp = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = "P16C2-T-1",
            FirstName = "Ana",
            LastName = "Test",
            Department = "Sewing",
            IsActive = true,
            HireDate = DateTime.UtcNow.AddYears(-1),
            TenantId = tenant.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "P16C2Test",
        };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();
        return emp.Id;
    }

    [Fact]
    public async Task Create_ThenList_ReturnsTheCertification()
    {
        var client = await AuthedAsync();
        var employeeId = await SeedEmployeeAsync();

        var create = await client.PostAsJsonAsync("/api/Hr/certifications", new
        {
            employeeId,
            certificationName = "Safety induction",
            skillArea = "Safety",
            issuedDate = DateTime.UtcNow.Date,
            expiryDate = DateTime.UtcNow.Date.AddYears(1),
            issuingAuthority = "External provider",
            certificateNumber = "SI-001",
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CertDto>>>(
            $"/api/Hr/certifications?employeeId={employeeId}");
        list!.Data!.Should().Contain(c => c.CertificationName == "Safety induction");
    }

    [Fact]
    public async Task Update_ChangesExpiryDate()
    {
        var client = await AuthedAsync();
        var employeeId = await SeedEmployeeAsync();
        var create = await client.PostAsJsonAsync("/api/Hr/certifications", new
        {
            employeeId,
            certificationName = "Forklift license",
            issuedDate = DateTime.UtcNow.Date,
            expiryDate = DateTime.UtcNow.Date.AddYears(2),
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<CertDto>>())!.Data!.Id;

        var newExpiry = DateTime.UtcNow.Date.AddYears(3);
        var put = await client.PutAsJsonAsync($"/api/Hr/certifications/{id}", new
        {
            id,
            certificationName = "Forklift license",
            issuedDate = DateTime.UtcNow.Date,
            expiryDate = newExpiry,
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CertDto>>>(
            $"/api/Hr/certifications?employeeId={employeeId}");
        list!.Data!.First(c => c.Id == id).ExpiryDate.Should().Be(newExpiry);
    }

    [Fact]
    public async Task Delete_SoftDeletes_NoLongerListed()
    {
        var client = await AuthedAsync();
        var employeeId = await SeedEmployeeAsync();
        var create = await client.PostAsJsonAsync("/api/Hr/certifications", new
        {
            employeeId,
            certificationName = "Disposable test cert",
            issuedDate = DateTime.UtcNow.Date,
        });
        var id = (await create.Content.ReadFromJsonAsync<ResultEnvelope<CertDto>>())!.Data!.Id;

        var del = await client.DeleteAsync($"/api/Hr/certifications/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CertDto>>>(
            $"/api/Hr/certifications?employeeId={employeeId}");
        list!.Data!.Should().NotContain(c => c.Id == id);
    }

    [Fact]
    public async Task Expiring_ReturnsOnlyCertsWithinWindow()
    {
        var client = await AuthedAsync();
        var employeeId = await SeedEmployeeAsync();

        // 10 days from now → inside a 30-day window.
        var inside = await client.PostAsJsonAsync("/api/Hr/certifications", new
        {
            employeeId,
            certificationName = "Expiring soon",
            issuedDate = DateTime.UtcNow.Date.AddYears(-1),
            expiryDate = DateTime.UtcNow.Date.AddDays(10),
        });
        inside.EnsureSuccessStatusCode();
        var insideId = (await inside.Content.ReadFromJsonAsync<ResultEnvelope<CertDto>>())!.Data!.Id;

        // 1000 days from now → outside.
        var outside = await client.PostAsJsonAsync("/api/Hr/certifications", new
        {
            employeeId,
            certificationName = "Expires later",
            issuedDate = DateTime.UtcNow.Date,
            expiryDate = DateTime.UtcNow.Date.AddDays(1000),
        });
        outside.EnsureSuccessStatusCode();
        var outsideId = (await outside.Content.ReadFromJsonAsync<ResultEnvelope<CertDto>>())!.Data!.Id;

        var expiring = await client.GetFromJsonAsync<ResultEnvelope<List<CertDto>>>(
            "/api/Hr/certifications/expiring?withinDays=30");
        expiring!.Data!.Should().Contain(c => c.Id == insideId);
        expiring.Data.Should().NotContain(c => c.Id == outsideId);
    }

    [Fact]
    public async Task TenantIsolation_OtherTenantsCertsAreHidden()
    {
        using var seedScope = _factory.Services.CreateScope();
        var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string foreignName = "FOREIGN-CERT-ISOLATION-TEST";

        // Clean from prior runs.
        var stale = await ctx.EmployeeCertifications.IgnoreQueryFilters()
            .Where(c => c.CertificationName == foreignName).ToListAsync();
        ctx.EmployeeCertifications.RemoveRange(stale);
        await ctx.SaveChangesAsync();

        var staleTenant = await ctx.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Code == "CERT-ISO-DEMO");
        if (staleTenant is not null)
        {
            var staleEmps = await ctx.Employees.IgnoreQueryFilters()
                .Where(e => e.TenantId == staleTenant.Id).ToListAsync();
            ctx.Employees.RemoveRange(staleEmps);
            ctx.Tenants.Remove(staleTenant);
        }
        await ctx.SaveChangesAsync();

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "CERT-ISO-DEMO",
            Name = "Cert Isolation Tenant",
            Country = "MK",
            DefaultLanguage = "mk",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "CertIsolationTest",
        };
        ctx.Tenants.Add(otherTenant);

        var foreignEmp = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = "CERT-ISO-EMP",
            FirstName = "Foreign",
            LastName = "Worker",
            Department = "Other",
            IsActive = true,
            HireDate = DateTime.UtcNow,
            TenantId = otherTenant.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "CertIsolationTest",
        };
        ctx.Employees.Add(foreignEmp);

        ctx.EmployeeCertifications.Add(new EmployeeCertification
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenant.Id,
            EmployeeId = foreignEmp.Id,
            CertificationName = foreignName,
            IssuedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "CertIsolationTest",
        });
        await ctx.SaveChangesAsync();

        var client = await AuthedAsync();
        var list = await client.GetFromJsonAsync<ResultEnvelope<List<CertDto>>>(
            "/api/Hr/certifications");
        list!.Data!.Should().NotContain(c => c.CertificationName == foreignName);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultEnvelope<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record CertDto(
        Guid Id,
        Guid EmployeeId,
        string? EmployeeName,
        string CertificationName,
        string? SkillArea,
        DateTime IssuedDate,
        DateTime? ExpiryDate);
}
