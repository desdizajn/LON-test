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
            .ToListAsync();

        return Ok(employees.Select(MapEmployee).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmployee(Guid id)
    {
        var employee = await _context.Employees
            .Include(e => e.User)
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
            Position = request.Position,
            Department = request.Department,
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
            .FirstAsync(e => e.Id == id);
    }

    private static EmployeeDto MapEmployee(Employee employee)
    {
        return new EmployeeDto(
            employee.Id,
            employee.UserId ?? Guid.Empty,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.Phone,
            employee.Position,
            employee.Department,
            employee.HireDate?.ToString("o") ?? string.Empty,
            employee.IsActive,
            employee.User == null
                ? null
                : new EmployeeUserDto(
                    employee.User.Username,
                    $"{employee.FirstName} {employee.LastName}".Trim()
                )
        );
    }

    private static string GenerateEmployeeNumber()
    {
        return $"EMP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }
}

public record EmployeeDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Position,
    string Department,
    string HireDate,
    bool IsActive,
    EmployeeUserDto? User
);

public record EmployeeUserDto(string Username, string FullName);

public record CreateEmployeeRequest(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Position,
    string Department,
    string HireDate
);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Position,
    string Department,
    bool IsActive
);
