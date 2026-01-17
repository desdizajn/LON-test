using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class RolesController : BaseController
{
    private readonly ApplicationDbContext _context;

    public RolesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        return Ok(roles.Select(MapRole).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRole(Guid id)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
        {
            return NotFound();
        }

        return Ok(MapRole(role));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] RoleRequest request)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = true
        };

        await _context.Roles.AddAsync(role);

        foreach (var permissionId in request.PermissionIds)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });
        }

        await _context.SaveChangesAsync();

        return Ok(MapRole(await LoadRole(role.Id)));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] RoleRequest request)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
        {
            return NotFound();
        }

        role.Name = request.Name;
        role.Description = request.Description;

        _context.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permissionId in request.PermissionIds)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });
        }

        await _context.SaveChangesAsync();

        return Ok(MapRole(await LoadRole(role.Id)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
        {
            return NotFound();
        }

        _context.RolePermissions.RemoveRange(role.RolePermissions);
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<Role> LoadRole(Guid id)
    {
        return await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == id);
    }

    private static RoleDto MapRole(Role role)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsActive,
            role.RolePermissions.Select(rp => PermissionMapper.Map(rp.Permission)).ToList()
        );
    }
}

public record RoleRequest(string Name, string? Description, List<Guid> PermissionIds);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    List<PermissionDto> Permissions
);
