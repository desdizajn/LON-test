using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace LON.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task<bool> InitializeAsync(
        ApplicationDbContext context,
        ILogger logger,
        int maxRetries = 10,
        int delaySeconds = 5)
    {
        var retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            try
            {
                logger.LogInformation("Attempting to connect to database (attempt {RetryCount}/{MaxRetries})...", 
                    retryCount + 1, maxRetries);
                
                // Test connection
                var canConnect = await context.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    throw new Exception("Cannot connect to database");
                }
                
                logger.LogInformation("Successfully connected to database.");
                
                // Check if database exists, if not create it
                logger.LogInformation("Ensuring database exists...");
                
                // Get pending migrations
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                var hasPendingMigrations = pendingMigrations.Any();
                
                if (hasPendingMigrations)
                {
                    logger.LogInformation("Found pending migrations. Applying...");
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully.");
                }
                else
                {
                    logger.LogInformation("Database is up to date. No pending migrations.");
                }
                
                return true; // Success
            }
            catch (Exception ex)
            {
                retryCount++;
                
                if (retryCount >= maxRetries)
                {
                    logger.LogError(ex, 
                        "Failed to initialize database after {MaxRetries} attempts.", maxRetries);
                    return false;
                }
                
                logger.LogWarning(ex, 
                    "Database initialization attempt {RetryCount} failed. Retrying in {Delay} seconds...", 
                    retryCount, delaySeconds);
                
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        
        return false;
    }
    
    public static async Task<bool> EnsureDatabaseExistsAsync(
        ApplicationDbContext context,
        ILogger logger)
    {
        try
        {
            // This will create the database if it doesn't exist
            var created = await context.Database.EnsureCreatedAsync();
            
            if (created)
            {
                logger.LogInformation("Database was created successfully.");
            }
            else
            {
                logger.LogInformation("Database already exists.");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure database exists.");
            return false;
        }
    }
}
