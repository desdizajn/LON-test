using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class PermissionsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public PermissionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _context.Permissions.ToListAsync();
        var result = permissions.Select(PermissionMapper.Map).ToList();
        return Ok(result);
    }
}

public record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    string Resource,
    string Action
);

public static class PermissionMapper
{
    public static PermissionDto Map(LON.Domain.Entities.MasterData.Permission permission)
    {
        var (resource, action) = SplitPermission(permission.Name, permission.Category);
        return new PermissionDto(
            permission.Id,
            permission.Name,
            permission.Description,
            resource,
            action
        );
    }

    private static (string Resource, string Action) SplitPermission(string name, string category)
    {
        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var resource = string.Join('.', parts.Take(parts.Length - 1));
            var action = parts[^1];
            return (resource, action);
        }

        return (category, name);
    }
}
