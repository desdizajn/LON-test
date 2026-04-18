using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Users.Commands.CreateUser;

/// <summary>
/// Provisions a new <see cref="User"/> for a tenant. Authorization (role
/// check) is enforced at the controller via <c>[Authorize(Roles=...)]</c>;
/// this handler trusts the caller but validates the target tenant.
///
/// TenantId semantics:
///   - If <see cref="TenantId"/> is null or <see cref="Guid.Empty"/>, the
///     DbContext auto-fill assigns the caller's tenant (same-tenant create).
///   - If provided, the user is created under that tenant. Cross-tenant
///     creation is allowed for callers with the <c>Administrator</c> role.
/// </summary>
public record CreateUserCommand : ICommand<Result<Guid>>
{
    public Guid? TenantId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public List<Guid> RoleIds { get; init; } = new();
}

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Result<Guid>.Failure("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<Guid>.Failure("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return Result<Guid>.Failure("Password is required.");

        // Username is globally unique (P1.5 — User.Username stays global pending
        // multi-tenant login UX decision in P1.7). Check across ALL tenants.
        var usernameTaken = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (usernameTaken)
            return Result<Guid>.Failure($"Username '{request.Username}' is already taken.");

        Guid? tenantId = request.TenantId == Guid.Empty ? null : request.TenantId;
        if (tenantId.HasValue)
        {
            var tenantOk = await _context.Tenants
                .AnyAsync(t => t.Id == tenantId.Value && t.IsActive && !t.IsDeleted, cancellationToken);
            if (!tenantOk)
                return Result<Guid>.Failure($"Tenant '{tenantId.Value}' does not exist or is inactive.");
        }

        List<Role> roles = new();
        if (request.RoleIds.Count > 0)
        {
            roles = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id) && r.IsActive)
                .ToListAsync(cancellationToken);
            if (roles.Count != request.RoleIds.Distinct().Count())
                return Result<Guid>.Failure("One or more role ids are invalid or inactive.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.Empty, // Guid.Empty → DbContext auto-fill = caller's tenant
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true
        };

        await _context.Users.AddAsync(user, cancellationToken);

        foreach (var role in roles)
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(user.Id);
    }
}
