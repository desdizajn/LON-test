using Microsoft.Data.SqlClient;
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
                logger.LogInformation("Applying migrations (attempt {RetryCount}/{MaxRetries}). Database will be created if it does not exist...",
                    retryCount + 1, maxRetries);

                // MigrateAsync will create the database if it is missing (avoids 'Cannot open database' errors)
                await context.Database.MigrateAsync();

                logger.LogInformation("Database is ready (migrations applied or already up to date).");
                return true; // Success
            }
            catch (SqlException ex) when (ex.Number is 4060 or 18456)
            {
                retryCount++;
                
                if (retryCount >= maxRetries)
                {
                    logger.LogError(ex,
                        "Failed to initialize database after {MaxRetries} attempts (SQL error {SqlError}).",
                        maxRetries, ex.Number);
                    return false;
                }

                logger.LogWarning(ex,
                    "Database initialization attempt {RetryCount} failed with SQL error {SqlError}. Retrying in {Delay} seconds...",
                    retryCount, ex.Number, delaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
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
