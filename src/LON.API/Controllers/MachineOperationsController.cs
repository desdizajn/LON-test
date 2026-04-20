using LON.Application.Machines;
using LON.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P11.1–P11.5 — machine operations: state events, downtime, maintenance.
/// Lives under <c>/api/Machines</c>; the master-data CRUD controller
/// (<see cref="MasterData.MachinesController"/>) stays at
/// <c>/api/MasterData/machines</c>.
/// </summary>
/// <remarks>
/// All request bodies are records with init-only properties, NOT positional
/// records. System.Text.Json can't bind positional record constructors from a
/// JSON body (see P6.42 regression) — kept as a hard rule for every body DTO
/// in this project.
/// </remarks>
[Route("api/Machines")]
public class MachineOperationsController : BaseController
{
    // P11.1 — state events ─────────────────────────────────────────

    public sealed record LogStateBody
    {
        public MachineState State { get; init; }
        public DateTime? ChangedAt { get; init; }
        public Guid? ChangedByEmployeeId { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPost("{id}/state-events")]
    public async Task<IActionResult> LogState(Guid id, [FromBody] LogStateBody body)
    {
        var result = await Mediator.Send(new LogMachineStateCommand(
            id, body.State, body.ChangedAt, body.ChangedByEmployeeId, body.Notes));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current-states")]
    public async Task<IActionResult> GetCurrentStates()
    {
        var result = await Mediator.Send(new GetCurrentMachineStatesQuery());
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // P11.2 — downtime ─────────────────────────────────────────────

    public sealed record LogDowntimeBody
    {
        public Guid MachineId { get; init; }
        public DateTime Start { get; init; }
        public DateTime? End { get; init; }
        public DowntimeCategory Category { get; init; }
        public string Reason { get; init; } = string.Empty;
        public decimal? CostImpact { get; init; }
        public Guid? ReportedByEmployeeId { get; init; }
    }

    [HttpPost("downtime")]
    public async Task<IActionResult> LogDowntime([FromBody] LogDowntimeBody body)
    {
        var result = await Mediator.Send(new LogDowntimeCommand(
            body.MachineId, body.Start, body.End, body.Category, body.Reason,
            body.CostImpact, body.ReportedByEmployeeId));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record CloseDowntimeBody
    {
        public DateTime End { get; init; }
    }

    [HttpPost("downtime/{id}/close")]
    public async Task<IActionResult> CloseDowntime(Guid id, [FromBody] CloseDowntimeBody body)
    {
        var result = await Mediator.Send(new CloseDowntimeCommand(id, body.End));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("downtime")]
    public async Task<IActionResult> GetDowntime(
        [FromQuery] Guid? machineId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetDowntimeEventsQuery(machineId, from, to));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("downtime/pareto")]
    public async Task<IActionResult> GetDowntimePareto(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetDowntimeParetoQuery(from, to));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // P11.4 — maintenance schedules ────────────────────────────────

    public sealed record CreateScheduleBody
    {
        public Guid MachineId { get; init; }
        public string TaskDescription { get; init; } = string.Empty;
        public int IntervalDays { get; init; }
        public DateTime? LastDone { get; init; }
        public DateTime? NextDue { get; init; }
    }

    [HttpPost("maintenance-schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleBody body)
    {
        var result = await Mediator.Send(new CreateMaintenanceScheduleCommand(
            body.MachineId, body.TaskDescription, body.IntervalDays,
            body.LastDone, body.NextDue));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record UpdateScheduleBody
    {
        public string TaskDescription { get; init; } = string.Empty;
        public int IntervalDays { get; init; }
        public DateTime? LastDone { get; init; }
        public DateTime NextDue { get; init; }
        public bool IsActive { get; init; }
    }

    [HttpPut("maintenance-schedules/{id}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] UpdateScheduleBody body)
    {
        var result = await Mediator.Send(new UpdateMaintenanceScheduleCommand(
            id, body.TaskDescription, body.IntervalDays,
            body.LastDone, body.NextDue, body.IsActive));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("maintenance-schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] bool? activeOnly = true)
    {
        var result = await Mediator.Send(new GetMaintenanceSchedulesQuery(activeOnly));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // P11.5 — work orders ──────────────────────────────────────────

    public sealed record CreateWorkOrderBody
    {
        public Guid MachineId { get; init; }
        public Guid? ScheduleId { get; init; }
        public DateTime ScheduledDate { get; init; }
        public Guid? TechnicianEmployeeId { get; init; }
        public string? TaskDescription { get; init; }
        public string? Notes { get; init; }
        public decimal? CostImpact { get; init; }
    }

    [HttpPost("maintenance-work-orders")]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderBody body)
    {
        var result = await Mediator.Send(new CreateMaintenanceWorkOrderCommand(
            body.MachineId, body.ScheduleId, body.ScheduledDate,
            body.TechnicianEmployeeId, body.TaskDescription, body.Notes, body.CostImpact));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record CompleteWorkOrderBody
    {
        public DateTime? CompletedAt { get; init; }
        public string? Notes { get; init; }
        public decimal? CostImpact { get; init; }
    }

    [HttpPost("maintenance-work-orders/{id}/complete")]
    public async Task<IActionResult> CompleteWorkOrder(Guid id, [FromBody] CompleteWorkOrderBody body)
    {
        var result = await Mediator.Send(new CompleteMaintenanceWorkOrderCommand(
            id, body.CompletedAt, body.Notes, body.CostImpact));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("maintenance-work-orders")]
    public async Task<IActionResult> GetWorkOrders(
        [FromQuery] Guid? machineId,
        [FromQuery] bool? openOnly)
    {
        var result = await Mediator.Send(new GetMaintenanceWorkOrdersQuery(machineId, openOnly));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
