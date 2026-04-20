using LON.Application.Hr;
using LON.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P10.1 / P10.2 / P10.5 — HR operations: attendance, absences, and
/// operator-machine assignments. Lives under <c>/api/Hr</c>.
/// </summary>
/// <remarks>
/// All body DTOs use init-only properties. Never switch to positional records
/// (see `feedback_positional_records_trap` memory — System.Text.Json can't
/// bind those from JSON bodies, and model validation fails before the
/// handler runs).
/// </remarks>
[Route("api/Hr")]
public class HrOperationsController : BaseController
{
    // P10.1 — attendance ───────────────────────────────────────────

    public sealed record ClockBody
    {
        public Guid EmployeeId { get; init; }
        public DateTime? At { get; init; }
    }

    [HttpPost("attendance/clock-in")]
    public async Task<IActionResult> ClockIn([FromBody] ClockBody body)
    {
        var result = await Mediator.Send(new ClockInCommand(body.EmployeeId, body.At));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("attendance/clock-out")]
    public async Task<IActionResult> ClockOut([FromBody] ClockBody body)
    {
        var result = await Mediator.Send(new ClockOutCommand(body.EmployeeId, body.At));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("attendance/today")]
    public async Task<IActionResult> GetToday([FromQuery] DateTime? day)
    {
        var result = await Mediator.Send(new GetAttendanceTodayQuery(day));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid? employeeId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetAttendanceHistoryQuery(employeeId, from, to));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // P10.2 — absences ─────────────────────────────────────────────

    public sealed record CreateAbsenceBody
    {
        public Guid EmployeeId { get; init; }
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public AbsenceType Type { get; init; }
        public string? Reason { get; init; }
    }

    [HttpPost("absences")]
    public async Task<IActionResult> CreateAbsence([FromBody] CreateAbsenceBody body)
    {
        var result = await Mediator.Send(new CreateAbsenceCommand(
            body.EmployeeId, body.From, body.To, body.Type, body.Reason));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record DecideAbsenceBody
    {
        public bool Approve { get; init; }
    }

    [HttpPost("absences/{id}/decide")]
    public async Task<IActionResult> DecideAbsence(Guid id, [FromBody] DecideAbsenceBody body)
    {
        var result = await Mediator.Send(new DecideAbsenceCommand(id, body.Approve));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("absences")]
    public async Task<IActionResult> GetAbsences(
        [FromQuery] Guid? employeeId,
        [FromQuery] bool? pendingOnly)
    {
        var result = await Mediator.Send(new GetAbsencesQuery(employeeId, pendingOnly));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // P10.5 — operator-machine assignments ─────────────────────────

    public sealed record CreateAssignmentBody
    {
        public Guid EmployeeId { get; init; }
        public Guid MachineId { get; init; }
        public DateTime ValidFrom { get; init; }
        public DateTime? ValidTo { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentBody body)
    {
        var result = await Mediator.Send(new CreateAssignmentCommand(
            body.EmployeeId, body.MachineId, body.ValidFrom, body.ValidTo, body.Notes));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record EndAssignmentBody
    {
        public DateTime ValidTo { get; init; }
    }

    [HttpPost("assignments/{id}/end")]
    public async Task<IActionResult> EndAssignment(Guid id, [FromBody] EndAssignmentBody body)
    {
        var result = await Mediator.Send(new EndAssignmentCommand(id, body.ValidTo));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? machineId,
        [FromQuery] bool? activeOnly)
    {
        var result = await Mediator.Send(new GetAssignmentsQuery(employeeId, machineId, activeOnly));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
