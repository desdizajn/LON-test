using System.Data.Common;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// WebApplicationFactory that swaps the real DB with a Testcontainers-managed
/// SQL Server instance. One container per test class (IAsyncLifetime pattern).
/// </summary>
public class LonApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Strong!TestPass123")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _sql.GetConnectionString(),
                ["JwtSettings:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-jwt",
                ["JwtSettings:Issuer"] = "LON.API.Test",
                ["JwtSettings:Audience"] = "LON.Client.Test",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["EnableVectorStore"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace DbContext with one bound to the Testcontainers connection.
            var desc = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (desc is not null) services.Remove(desc);

            var connDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));
            if (connDesc is not null) services.Remove(connDesc);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_sql.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        // Pre-build host so startup migrations + seeding run against the test DB.
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sql.DisposeAsync();
    }
}
