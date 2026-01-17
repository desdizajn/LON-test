using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using LON.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class UsersController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;

    public UsersController(ApplicationDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .Include(u => u.Employee)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        var result = users.Select(MapUser).ToList();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Employee)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(MapUser(user));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new { message = "Корисничкото име веќе постои." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _authService.HashPassword(request.Password),
            IsActive = true
        };

        await _context.Users.AddAsync(user);

        if (request.RoleIds.Any())
        {
            var roles = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id))
                .ToListAsync();

            foreach (var role in roles)
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        await _context.SaveChangesAsync();

        return Ok(MapUser(await LoadUser(user.Id)));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Employee)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        user.Email = request.Email;
        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.FullName) && user.Employee != null)
        {
            var nameParts = request.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length > 0)
            {
                user.Employee.FirstName = nameParts[0];
                user.Employee.LastName = nameParts.Length > 1
                    ? string.Join(' ', nameParts.Skip(1))
                    : string.Empty;
            }
        }

        _context.UserRoles.RemoveRange(user.UserRoles);
        foreach (var roleId in request.RoleIds)
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId
            });
        }

        await _context.SaveChangesAsync();

        return Ok(MapUser(await LoadUser(user.Id)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/change-password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        if (!_authService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Тековната лозинка не е точна." });
        }

        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<User> LoadUser(Guid id)
    {
        return await _context.Users
            .Include(u => u.Employee)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstAsync(u => u.Id == id);
    }

    private static UserDto MapUser(User user)
    {
        var roleIds = user.UserRoles.Select(ur => ur.RoleId.ToString()).ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToList();

        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.Employee != null
                ? $"{user.Employee.FirstName} {user.Employee.LastName}".Trim()
                : user.Username,
            user.IsActive,
            roleIds,
            permissions,
            user.LastLoginAt,
            user.CreatedAt
        );
    }
}

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    bool IsActive,
    List<string> Roles,
    List<string> Permissions,
    DateTime? LastLogin,
    DateTime CreatedAt
);

public record CreateUserRequest(
    string Username,
    string Email,
    string FullName,
    string Password,
    List<Guid> RoleIds
);

public record UpdateUserRequest(
    string Email,
    string FullName,
    bool IsActive,
    List<Guid> RoleIds
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
