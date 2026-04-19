using LON.Infrastructure;
using LON.Infrastructure.Initialization;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // EF navigation properties create object cycles (e.g. Receipt.Lines[*].Receipt).
        // IgnoreCycles drops the back-reference rather than looping or erroring.
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LON Production + WMS + Customs API",
        Version = "v1",
        Description = "Enterprise system for Production, WMS, Customs & Trade Compliance, Guarantee Management, and BI Analytics"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Current user (audit) — depends on IHttpContextAccessor.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LON.Application.Common.Interfaces.ICurrentUserService, LON.API.Services.CurrentUserService>();
builder.Services.AddScoped<LON.Application.Common.Interfaces.ICurrentTenantService, LON.API.Services.CurrentTenantService>();

// Add MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LON.Application.Common.Commands.ICommand<>).Assembly);
});

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "LON.API",
        ValidAudience = jwtSettings["Audience"] ?? "LON.Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// ──────────── DataProtection (P6.16) ────────────
// Make the key ring configuration explicit so startup no longer logs
// "No XML encryptor configured. Key {id} may be persisted to storage in
// unencrypted form." Keys persist to the docker volume
// `lon_dataprotection_keys` (mounted at `/root/.aspnet/DataProtection-Keys`).
// Setting ApplicationName pins the key-ring discriminator so tokens keep
// working across container recreations. Certificate-based encryption is
// deferred until we have a cert-management story on VPS.
builder.Services.AddDataProtection()
    .SetApplicationName("LON-API")
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize database with retry logic
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();
    
    logger.LogInformation("Starting database initialization...");
    
    // Initialize database with retry logic
    var initialized = await LON.Infrastructure.Persistence.DatabaseInitializer.InitializeAsync(
        context, 
        logger, 
        maxRetries: 10, 
        delaySeconds: 5);
    
    if (initialized)
    {
        try
        {
            // Seed tenants FIRST — everything else is tenant-scoped and needs
            // at least one Tenant row for the SaveChangesAsync auto-fill.
            logger.LogInformation("Seeding tenants...");
            await ApplicationDbContextSeed.SeedTenantsAsync(context);

            // Seed User Management data (critical for login)
            logger.LogInformation("Seeding user management data...");
            var authService = services.GetRequiredService<LON.Infrastructure.Services.IAuthService>();
            await UserManagementSeed.SeedAsync(context, authService, logger);

            // P6.37.14: top up roles + test users that weren't in the original
            // bootstrap seed. Idempotent — safe on existing DBs.
            await RoleTopUpSeed.SeedAsync(context, authService, logger);

            // Seed master data (without Knowledge Base - that runs in background)
            logger.LogInformation("Seeding master data...");
            await ApplicationDbContextSeed.SeedAsync(context, skipKnowledgeBase: true);

            // ✅ Vector Store сега се иницијализира во background преку VectorStoreBackgroundService
            logger.LogInformation("Vector Store ќе се иницијализира во background (ако е enable-ирано).");

            logger.LogInformation("✅ Database initialization completed successfully.");

            // Knowledge Base seeding runs in background - does not block API startup
            _ = Task.Run(async () =>
            {
                try
                {
                    using var kbScope = app.Services.CreateScope();
                    var kbContext = kbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var kbLogger = kbScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    kbLogger.LogInformation("Starting Knowledge Base seeding in background...");
                    await KnowledgeBaseSeeder.SeedKnowledgeBaseAsync(kbContext);
                    kbLogger.LogInformation("✅ Knowledge Base seeding completed in background.");
                }
                catch (Exception kbEx)
                {
                    var kbLogger = app.Services.GetRequiredService<ILogger<Program>>();
                    kbLogger.LogWarning(kbEx, "Knowledge Base seeding failed in background. This is non-critical.");
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during data seeding. Application will continue but data may be incomplete.");
        }
    }
    else
    {
        logger.LogError("❌ Failed to initialize database. Application will start but database operations may fail.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ──────────────── Health checks (P6.15) ────────────────
// K8s-style split:
//   /health/live   — liveness: process is up (always 200 unless the host is
//                    actively falling over). Caddy / Compose should restart
//                    the container only when this fails.
//   /health/ready  — readiness: can the app serve traffic? DB probe.
//                    Load balancer should withhold traffic during a red status
//                    rather than restart the container.
//
// /health and /health/db kept as deprecated aliases so existing dashboards
// keep working while consumers migrate.

static IResult HealthLive() => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
});

static async Task<IResult> HealthReady(ApplicationDbContext context, ILoggerFactory loggerFactory)
{
    var logger = loggerFactory.CreateLogger("HealthReady");
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "ready", database = "connected", timestamp = DateTime.UtcNow })
            : Results.Json(
                new { status = "not-ready", database = "disconnected", timestamp = DateTime.UtcNow },
                statusCode: 503);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Readiness DB probe failed");
        return Results.Json(
            new { status = "not-ready", database = "error", error = ex.Message, timestamp = DateTime.UtcNow },
            statusCode: 503);
    }
}

app.MapGet("/health/live", HealthLive);
app.MapGet("/health/ready", HealthReady);

// Backwards-compat aliases (will be removed once monitoring targets migrate).
app.MapGet("/health", HealthLive);
app.MapGet("/health/db", HealthReady);

app.Run();

// Marker for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
