using AppDtos = LON.Application.Common.DTOs;
using LON.Infrastructure.Persistence;
using LON.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ApplicationDbContext context,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AppDtos.LoginResponse>> Login([FromBody] AppDtos.LoginRequest request)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e!.Shift)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

            if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            var roles = user.UserRoles.Where(ur => ur.Role.IsActive).Select(ur => ur.Role.Name).ToList();
            var permissions = user.UserRoles
                .Where(ur => ur.Role.IsActive)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

            var accessToken = _authService.GenerateJwtToken(user, roles, permissions);
            var refreshToken = _authService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            user.LastLoginAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var expiresAt = DateTime.UtcNow.AddHours(1); // Should match JWT expiry

            var response = new AppDtos.LoginResponse(
                accessToken,
                refreshToken,
                expiresAt,
                new AppDtos.UserDto(
                    user.Id,
                    user.Username,
                    user.Email,
                    user.IsActive,
                    user.LastLoginAt,
                    user.Employee != null ? new AppDtos.EmployeeDto(
                        user.Employee.Id,
                        user.Employee.EmployeeNumber,
                        user.Employee.FirstName,
                        user.Employee.LastName,
                        user.Employee.Email,
                        user.Employee.Phone,
                        user.Employee.Department,
                        user.Employee.Position,
                        user.Employee.HireDate,
                        user.Employee.Shift != null ? new AppDtos.ShiftDto(
                            user.Employee.Shift.Id,
                            user.Employee.Shift.Code,
                            user.Employee.Shift.Name,
                            user.Employee.Shift.StartTime,
                            user.Employee.Shift.EndTime,
                            user.Employee.Shift.Description,
                            user.Employee.Shift.IsActive
                        ) : null,
                        user.Employee.IsActive
                    ) : null,
                    user.UserRoles.Where(ur => ur.Role.IsActive).Select(ur => new AppDtos.RoleDto(
                        ur.Role.Id,
                        ur.Role.Name,
                        ur.Role.Description,
                        ur.Role.IsActive,
                        ur.Role.RolePermissions.Select(rp => new AppDtos.PermissionDto(
                            rp.Permission.Id,
                            rp.Permission.Name,
                            rp.Permission.Description,
                            rp.Permission.Category
                        )).ToList()
                    )).ToList(),
                    user.CreatedAt
                )
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AppDtos.LoginResponse>> Refresh([FromBody] AppDtos.RefreshTokenRequest request)
    {
        try
        {
            var user = await _authService.ValidateRefreshTokenAsync(request.RefreshToken);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid or expired refresh token" });
            }

            // Load full user with relations
            user = await _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e!.Shift)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            var roles = user.UserRoles.Where(ur => ur.Role.IsActive).Select(ur => ur.Role.Name).ToList();
            var permissions = user.UserRoles
                .Where(ur => ur.Role.IsActive)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

            var accessToken = _authService.GenerateJwtToken(user, roles, permissions);
            var newRefreshToken = _authService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            var expiresAt = DateTime.UtcNow.AddHours(1);

            var response = new AppDtos.LoginResponse(
                accessToken,
                newRefreshToken,
                expiresAt,
                new AppDtos.UserDto(
                    user.Id,
                    user.Username,
                    user.Email,
                    user.IsActive,
                    user.LastLoginAt,
                    user.Employee != null ? new AppDtos.EmployeeDto(
                        user.Employee.Id,
                        user.Employee.EmployeeNumber,
                        user.Employee.FirstName,
                        user.Employee.LastName,
                        user.Employee.Email,
                        user.Employee.Phone,
                        user.Employee.Department,
                        user.Employee.Position,
                        user.Employee.HireDate,
                        user.Employee.Shift != null ? new AppDtos.ShiftDto(
                            user.Employee.Shift.Id,
                            user.Employee.Shift.Code,
                            user.Employee.Shift.Name,
                            user.Employee.Shift.StartTime,
                            user.Employee.Shift.EndTime,
                            user.Employee.Shift.Description,
                            user.Employee.Shift.IsActive
                        ) : null,
                        user.Employee.IsActive
                    ) : null,
                    user.UserRoles.Where(ur => ur.Role.IsActive).Select(ur => new AppDtos.RoleDto(
                        ur.Role.Id,
                        ur.Role.Name,
                        ur.Role.Description,
                        ur.Role.IsActive,
                        ur.Role.RolePermissions.Select(rp => new AppDtos.PermissionDto(
                            rp.Permission.Id,
                            rp.Permission.Name,
                            rp.Permission.Description,
                            rp.Permission.Category
                        )).ToList()
                    )).ToList(),
                    user.CreatedAt
                )
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { message = "An error occurred during token refresh" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout()
    {
        try
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AppDtos.UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = GetUserId();
            var user = await _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e!.Shift)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var response = new AppDtos.UserDto(
                user.Id,
                user.Username,
                user.Email,
                user.IsActive,
                user.LastLoginAt,
                user.Employee != null ? new AppDtos.EmployeeDto(
                    user.Employee.Id,
                    user.Employee.EmployeeNumber,
                    user.Employee.FirstName,
                    user.Employee.LastName,
                    user.Employee.Email,
                    user.Employee.Phone,
                    user.Employee.Department,
                    user.Employee.Position,
                    user.Employee.HireDate,
                    user.Employee.Shift != null ? new AppDtos.ShiftDto(
                        user.Employee.Shift.Id,
                        user.Employee.Shift.Code,
                        user.Employee.Shift.Name,
                        user.Employee.Shift.StartTime,
                        user.Employee.Shift.EndTime,
                        user.Employee.Shift.Description,
                        user.Employee.Shift.IsActive
                    ) : null,
                    user.Employee.IsActive
                ) : null,
                user.UserRoles.Where(ur => ur.Role.IsActive).Select(ur => new AppDtos.RoleDto(
                    ur.Role.Id,
                    ur.Role.Name,
                    ur.Role.Description,
                    ur.Role.IsActive,
                    ur.Role.RolePermissions.Select(rp => new AppDtos.PermissionDto(
                        rp.Permission.Id,
                        rp.Permission.Name,
                        rp.Permission.Description,
                        rp.Permission.Category
                    )).ToList()
                )).ToList(),
                user.CreatedAt
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { message = "An error occurred while retrieving user" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim!.Value);
    }
}
