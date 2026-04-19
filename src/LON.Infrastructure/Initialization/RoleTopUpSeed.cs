using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using LON.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Initialization;

/// <summary>
/// Idempotent top-up for roles + test users referenced by the P6.37 role-aware
/// sidebar. Runs on every startup. Unlike <see cref="UserManagementSeed"/>
/// (which short-circuits once any user exists and therefore never re-runs on
/// an existing VPS DB), this seeder checks per-row and adds only what's
/// missing. Safe to re-invoke.
///
/// Adds 8 roles (Customs Officer, Warehouse Operator, Production Operator,
/// Quality Controller, HR Manager, Maintenance Tech, Finance Clerk, Manager)
/// and 8 matching TEKSPORT-scoped test users so that per-role sidebar
/// filtering can be verified end-to-end on the VPS.
/// </summary>
public static class RoleTopUpSeed
{
    private sealed record RoleSpec(string Name, string Description);

    private static readonly RoleSpec[] Roles =
    {
        new("Customs Officer", "Царински референт — декларации, MRN, рокови"),
        new("Warehouse Operator", "Магационер — физичка работа на прием/издавање"),
        new("Production Operator", "Производствен работник — извршува налози"),
        new("Quality Controller", "Контрола на квалитет — hold/release/reject"),
        new("HR Manager", "Човечки ресурси — вработени, смени, изостаноци"),
        new("Maintenance Tech", "Одржување на машини — PM, downtime, интервенции"),
        new("Finance Clerk", "Финансии — invoicing, контра, AP/AR"),
        new("Manager", "Раководител — KPI, ризици, клиенти, margin"),
    };

    private sealed record TestUserSpec(string Username, string Email, string RoleName);

    private const string TestUserPassword = "Test123!";
    private const string TenantCode = "TEKSPORT";

    private static readonly TestUserSpec[] TestUsers =
    {
        new("tek-customs", "customs@tekuser.local", "Customs Officer"),
        new("tek-wh-op", "wh-op@tekuser.local", "Warehouse Operator"),
        new("tek-operator", "operator@tekuser.local", "Production Operator"),
        new("tek-qc", "qc@tekuser.local", "Quality Controller"),
        new("tek-hr", "hr@tekuser.local", "HR Manager"),
        new("tek-maint", "maint@tekuser.local", "Maintenance Tech"),
        new("tek-finance", "finance@tekuser.local", "Finance Clerk"),
        new("tek-mgr", "mgr@tekuser.local", "Manager"),
    };

    public static async Task SeedAsync(ApplicationDbContext context, IAuthService authService, ILogger logger)
    {
        var addedRoles = await TopUpRolesAsync(context, logger);
        await TopUpRolePermissionsAsync(context, logger);
        await TopUpTestUsersAsync(context, authService, logger);

        if (addedRoles > 0)
        {
            logger.LogInformation("RoleTopUpSeed: added {Count} missing roles.", addedRoles);
        }
    }

    private static async Task<int> TopUpRolesAsync(ApplicationDbContext context, ILogger logger)
    {
        var existing = await context.Roles.Select(r => r.Name).ToListAsync();
        var missing = Roles.Where(r => !existing.Contains(r.Name)).ToList();
        if (missing.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var spec in missing)
        {
            context.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                Name = spec.Name,
                Description = spec.Description,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "RoleTopUpSeed",
            });
        }
        await context.SaveChangesAsync();
        return missing.Count;
    }

    /// <summary>
    /// Every new role gets read-only access (all *.View permissions) so its
    /// JWT carries enough authority to see the sidebar groups without ever
    /// hitting a 403. Category-specific write permissions are handled case by
    /// case; for now View-only is sufficient for UI verification.
    /// </summary>
    private static async Task TopUpRolePermissionsAsync(ApplicationDbContext context, ILogger logger)
    {
        var allPermissions = await context.Permissions.ToListAsync();
        if (allPermissions.Count == 0) return; // UserManagementSeed hasn't run yet

        var viewPermissions = allPermissions.Where(p => p.Name.EndsWith(".View")).ToList();

        foreach (var spec in Roles)
        {
            var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == spec.Name);
            if (role is null) continue;

            var existingPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var toAdd = viewPermissions
                .Where(p => !existingPermIds.Contains(p.Id))
                .Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id })
                .ToList();

            if (toAdd.Count > 0) context.RolePermissions.AddRange(toAdd);
        }

        await context.SaveChangesAsync();
    }

    private static async Task TopUpTestUsersAsync(ApplicationDbContext context, IAuthService authService, ILogger logger)
    {
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Code == TenantCode);
        if (tenant is null)
        {
            logger.LogWarning("RoleTopUpSeed: tenant {Code} not found; skipping test users.", TenantCode);
            return;
        }

        var now = DateTime.UtcNow;
        var addedUsers = 0;

        foreach (var spec in TestUsers)
        {
            if (await context.Users.AnyAsync(u => u.Username == spec.Username)) continue;

            var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == spec.RoleName);
            if (role is null)
            {
                logger.LogWarning("RoleTopUpSeed: role {Role} not found for user {User}.", spec.RoleName, spec.Username);
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Username = spec.Username,
                Email = spec.Email,
                PasswordHash = authService.HashPassword(TestUserPassword),
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "RoleTopUpSeed",
            };
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            addedUsers++;
        }

        if (addedUsers > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("RoleTopUpSeed: created {Count} test users (password: {Password}).", addedUsers, TestUserPassword);
        }
    }
}
