using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class EmployeesController : BaseController
{
    private readonly ApplicationDbContext _context;

    public EmployeesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.DepartmentRef)
            .Include(e => e.PositionRef)
            .ToListAsync();

        return Ok(employees.Select(MapEmployee).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmployee(Guid id)
    {
        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.DepartmentRef)
            .Include(e => e.PositionRef)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
        {
            return NotFound();
        }

        return Ok(MapEmployee(employee));
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
        if (user == null)
        {
            return BadRequest(new { message = "Корисникот не постои." });
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            EmployeeNumber = GenerateEmployeeNumber(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            // Phase 17 §E7.5 — DepartmentId/PositionId are the new canonical
            // fields. Position/Department free-text persist for the deprecation
            // window so legacy importers and reports keep working.
            Position = request.Position,
            Department = request.Department,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            HireDate = DateTime.Parse(request.HireDate),
            IsActive = true
        };

        user.EmployeeId = employee.Id;
        await _context.Employees.AddAsync(employee);
        await _context.SaveChangesAsync();

        return Ok(MapEmployee(await LoadEmployee(employee.Id)));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeRequest request)
    {
        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
        {
            return NotFound();
        }

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.Email = request.Email;
        employee.Phone = request.Phone;
        employee.Position = request.Position;
        employee.Department = request.Department;
        employee.DepartmentId = request.DepartmentId;
        employee.PositionId = request.PositionId;
        employee.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(MapEmployee(await LoadEmployee(employee.Id)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEmployee(Guid id)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee == null)
        {
            return NotFound();
        }

        employee.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<Employee> LoadEmployee(Guid id)
    {
        return await _context.Employees
            .Include(e => e.User)
            .Include(e => e.DepartmentRef)
            .Include(e => e.PositionRef)
            .FirstAsync(e => e.Id == id);
    }

    private static EmployeeDto MapEmployee(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            UserId = employee.UserId ?? Guid.Empty,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            Phone = employee.Phone,
            Position = employee.Position ?? string.Empty,
            Department = employee.Department ?? string.Empty,
            DepartmentId = employee.DepartmentId,
            DepartmentName = employee.DepartmentRef?.DescriptionMK ?? employee.DepartmentRef?.Code,
            PositionId = employee.PositionId,
            PositionName = employee.PositionRef?.DescriptionMK ?? employee.PositionRef?.Code,
            HireDate = employee.HireDate?.ToString("o") ?? string.Empty,
            IsActive = employee.IsActive,
            User = employee.User == null
                ? null
                : new EmployeeUserDto
                {
                    Username = employee.User.Username,
                    FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
                },
        };
    }

    private static string GenerateEmployeeNumber()
    {
        return $"EMP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }
}

// Phase 17 §E7.5 — init-only properties (not positional records) so System.Text.Json
// can bind partial bodies. See `feedback_positional_records_trap.md` memory.
public record EmployeeDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Position { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public Guid? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public Guid? PositionId { get; init; }
    public string? PositionName { get; init; }
    public string HireDate { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public EmployeeUserDto? User { get; init; }
}

public record EmployeeUserDto
{
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}

public record CreateEmployeeRequest
{
    public Guid UserId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Position { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public string HireDate { get; init; } = string.Empty;
}

public record UpdateEmployeeRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Position { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public bool IsActive { get; init; }
}
