using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using LON.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Initialization;

public static class UserManagementSeed
{
    public static async Task SeedAsync(ApplicationDbContext context, IAuthService authService, ILogger logger)
    {
        try
        {
            // Check if any users exist
            if (await context.Users.AnyAsync())
            {
                logger.LogInformation("User management data already seeded.");
                return;
            }

            logger.LogInformation("Starting user management seed...");

            // Create Permissions
            var permissions = new List<Permission>
            {
                // Master Data
                new() { Id = Guid.NewGuid(), Name = "MasterData.Items.View", Description = "View items", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Items.Create", Description = "Create items", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Items.Edit", Description = "Edit items", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Items.Delete", Description = "Delete items", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Partners.View", Description = "View partners", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Partners.Create", Description = "Create partners", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Partners.Edit", Description = "Edit partners", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.Partners.Delete", Description = "Delete partners", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.BOMs.View", Description = "View BOMs", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.BOMs.Create", Description = "Create BOMs", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.BOMs.Edit", Description = "Edit BOMs", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "MasterData.BOMs.Delete", Description = "Delete BOMs", Category = "MasterData", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                
                // Production
                new() { Id = Guid.NewGuid(), Name = "Production.Orders.View", Description = "View production orders", Category = "Production", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Production.Orders.Create", Description = "Create production orders", Category = "Production", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Production.Orders.Edit", Description = "Edit production orders", Category = "Production", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Production.Orders.Delete", Description = "Delete production orders", Category = "Production", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Production.Execution.View", Description = "View production execution", Category = "Production", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Production.Execution.Record", Description = "Record production", Category = "Production", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                
                // WMS
                new() { Id = Guid.NewGuid(), Name = "WMS.Inventory.View", Description = "View inventory", Category = "WMS", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "WMS.Movements.Record", Description = "Record inventory movements", Category = "WMS", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "WMS.Picking.Execute", Description = "Execute picking", Category = "WMS", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "WMS.Receiving.Execute", Description = "Execute receiving", Category = "WMS", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                
                // Customs
                new() { Id = Guid.NewGuid(), Name = "Customs.Declarations.View", Description = "View customs declarations", Category = "Customs", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Customs.Declarations.Create", Description = "Create customs declarations", Category = "Customs", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Customs.Declarations.Submit", Description = "Submit customs declarations", Category = "Customs", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                
                // Administration
                new() { Id = Guid.NewGuid(), Name = "Admin.Users.View", Description = "View users", Category = "Administration", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Admin.Users.Create", Description = "Create users", Category = "Administration", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Admin.Users.Edit", Description = "Edit users", Category = "Administration", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Admin.Users.Delete", Description = "Delete users", Category = "Administration", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
                new() { Id = Guid.NewGuid(), Name = "Admin.Roles.Manage", Description = "Manage roles and permissions", Category = "Administration", CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            };

            await context.Permissions.AddRangeAsync(permissions);
            await context.SaveChangesAsync();
            logger.LogInformation($"Seeded {permissions.Count} permissions.");

            // Create Roles
            var adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Administrator",
                Description = "Full system access",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            var warehouseManagerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Warehouse Manager",
                Description = "Manage warehouse operations",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            var productionManagerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Production Manager",
                Description = "Manage production operations",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            var viewerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Viewer",
                Description = "Read-only access",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            await context.Roles.AddRangeAsync(new[] { adminRole, warehouseManagerRole, productionManagerRole, viewerRole });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded 4 roles.");

            // Assign all permissions to Administrator
            var adminPermissions = permissions.Select(p => new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = p.Id
            }).ToList();

            await context.RolePermissions.AddRangeAsync(adminPermissions);

            // Assign WMS permissions to Warehouse Manager
            var warehousePermissions = permissions
                .Where(p => p.Category == "WMS" || p.Name.StartsWith("MasterData.Items") || p.Name.StartsWith("MasterData.Partners"))
                .Select(p => new RolePermission
                {
                    RoleId = warehouseManagerRole.Id,
                    PermissionId = p.Id
                })
                .ToList();

            await context.RolePermissions.AddRangeAsync(warehousePermissions);

            // Assign Production permissions to Production Manager
            var productionPermissions = permissions
                .Where(p => p.Category == "Production" || p.Name.StartsWith("MasterData"))
                .Select(p => new RolePermission
                {
                    RoleId = productionManagerRole.Id,
                    PermissionId = p.Id
                })
                .ToList();

            await context.RolePermissions.AddRangeAsync(productionPermissions);

            // Assign View permissions to Viewer
            var viewPermissions = permissions
                .Where(p => p.Name.Contains("View"))
                .Select(p => new RolePermission
                {
                    RoleId = viewerRole.Id,
                    PermissionId = p.Id
                })
                .ToList();

            await context.RolePermissions.AddRangeAsync(viewPermissions);
            await context.SaveChangesAsync();
            logger.LogInformation("Assigned permissions to roles.");

            // Create Shifts
            var morningShift = new Shift
            {
                Id = Guid.NewGuid(),
                Code = "MORNING",
                Name = "Morning Shift",
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(16, 0, 0),
                Description = "Morning shift 08:00-16:00",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            var afternoonShift = new Shift
            {
                Id = Guid.NewGuid(),
                Code = "AFTERNOON",
                Name = "Afternoon Shift",
                StartTime = new TimeSpan(16, 0, 0),
                EndTime = new TimeSpan(0, 0, 0), // Midnight
                Description = "Afternoon shift 16:00-00:00",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            var nightShift = new Shift
            {
                Id = Guid.NewGuid(),
                Code = "NIGHT",
                Name = "Night Shift",
                StartTime = new TimeSpan(0, 0, 0), // Midnight
                EndTime = new TimeSpan(8, 0, 0),
                Description = "Night shift 00:00-08:00",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            await context.Shifts.AddRangeAsync(new[] { morningShift, afternoonShift, nightShift });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded 3 shifts.");

            // Create Admin User
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@lon.local",
                PasswordHash = authService.HashPassword("Admin123!"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();
            logger.LogInformation("Created admin user.");

            // Assign Administrator role to admin user
            var adminUserRole = new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            };

            await context.UserRoles.AddAsync(adminUserRole);
            await context.SaveChangesAsync();
            logger.LogInformation("Assigned Administrator role to admin user.");

            logger.LogInformation("User management seed completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while seeding user management data.");
            throw;
        }
    }
}
