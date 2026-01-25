using LON.Infrastructure;
using LON.Infrastructure.Initialization;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
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
            // Seed master data
            logger.LogInformation("Seeding master data...");
            await ApplicationDbContextSeed.SeedAsync(context);
            
            // Seed User Management data
            logger.LogInformation("Seeding user management data...");
            var authService = services.GetRequiredService<LON.Infrastructure.Services.IAuthService>();
            await UserManagementSeed.SeedAsync(context, authService, logger);
            
            // ✅ Vector Store сега се иницијализира во background преку VectorStoreBackgroundService
            // Ова го овозможува брзо стартување на API без да чека на долгата иницијализација
            logger.LogInformation("Vector Store ќе се иницијализира во background (ако е enable-ирано).");
            
            logger.LogInformation("✅ Database initialization completed successfully.");
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

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Database health check endpoint
app.MapGet("/health/db", async (ApplicationDbContext context) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            return Results.Ok(new
            {
                status = "healthy",
                database = "connected",
                timestamp = DateTime.UtcNow
            });
        }
        else
        {
            return Results.Json(new
            {
                status = "unhealthy",
                database = "disconnected",
                timestamp = DateTime.UtcNow
            }, statusCode: 503);
        }
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "unhealthy",
            database = "error",
            error = ex.Message,
            timestamp = DateTime.UtcNow
        }, statusCode: 503);
    }
});

app.Run();
