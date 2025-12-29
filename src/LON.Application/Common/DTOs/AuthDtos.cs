namespace LON.Application.Common.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RefreshTokenRequest(string RefreshToken);

public record RegisterUserRequest(
    string Username,
    string Email,
    string Password,
    Guid? EmployeeId,
    List<Guid> RoleIds
);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    DateTime? LastLoginAt,
    EmployeeDto? Employee,
    List<RoleDto> Roles,
    DateTime CreatedAt
);

public record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Department,
    string? Position,
    DateTime? HireDate,
    ShiftDto? Shift,
    bool IsActive
);

public record ShiftDto(
    Guid Id,
    string Code,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? Description,
    bool IsActive
);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    List<PermissionDto>? Permissions
);

public record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    string Category
);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    Guid? EmployeeId,
    List<Guid> RoleIds,
    bool IsActive = true
);

public record UpdateUserRequest(
    string Email,
    Guid? EmployeeId,
    bool IsActive
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record CreateRoleRequest(
    string Name,
    string? Description,
    List<Guid> PermissionIds
);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive
);

public record CreateShiftRequest(
    string Code,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? Description
);

public record UpdateShiftRequest(
    string Code,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? Description,
    bool IsActive
);

public record CreateEmployeeRequest(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Department,
    string? Position,
    DateTime? HireDate,
    Guid? ShiftId
);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Department,
    string? Position,
    DateTime? HireDate,
    Guid? ShiftId,
    bool IsActive
);
