using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LON.Infrastructure.Services;

public interface IAuthService
{
    string GenerateJwtToken(User user, List<string> roles, List<string> permissions);
    string GenerateRefreshToken();
    Task<User?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly LON.Infrastructure.Persistence.ApplicationDbContext _context;

    public AuthService(IConfiguration configuration, LON.Infrastructure.Persistence.ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public string GenerateJwtToken(User user, List<string> roles, List<string> permissions)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("EmployeeId", user.EmployeeId?.ToString() ?? string.Empty)
        };

        // Add roles
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Add permissions
        claims.AddRange(permissions.Select(permission => new Claim("Permission", permission)));

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:ExpiryMinutes"])),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<User?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && 
                                     u.RefreshTokenExpiryTime > DateTime.UtcNow &&
                                     u.IsActive, 
                                 cancellationToken);

        return user;
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var permissions = await _context.Users
            .Where(u => u.Id == userId && u.IsActive)
            .SelectMany(u => u.UserRoles)
            .Where(ur => ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }
}
