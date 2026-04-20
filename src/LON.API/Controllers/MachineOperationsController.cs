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
[Route("api/Machines")]
public class MachineOperationsController : BaseController
{
    // P11.1 — state events ─────────────────────────────────────────

    public sealed record LogStateBody(
        MachineState State,
        DateTime? ChangedAt,
        Guid? ChangedByEmployeeId,
        string? Notes);

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

    public sealed record LogDowntimeBody(
        Guid MachineId,
        DateTime Start,
        DateTime? End,
        DowntimeCategory Category,
        string Reason,
        decimal? CostImpact,
        Guid? ReportedByEmployeeId);

    [HttpPost("downtime")]
    public async Task<IActionResult> LogDowntime([FromBody] LogDowntimeBody body)
    {
        var result = await Mediator.Send(new LogDowntimeCommand(
            body.MachineId, body.Start, body.End, body.Category, body.Reason,
            body.CostImpact, body.ReportedByEmployeeId));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record CloseDowntimeBody(DateTime End);

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

    public sealed record CreateScheduleBody(
        Guid MachineId,
        string TaskDescription,
        int IntervalDays,
        DateTime? LastDone,
        DateTime? NextDue);

    [HttpPost("maintenance-schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleBody body)
    {
        var result = await Mediator.Send(new CreateMaintenanceScheduleCommand(
            body.MachineId, body.TaskDescription, body.IntervalDays,
            body.LastDone, body.NextDue));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record UpdateScheduleBody(
        string TaskDescription,
        int IntervalDays,
        DateTime? LastDone,
        DateTime NextDue,
        bool IsActive);

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

    public sealed record CreateWorkOrderBody(
        Guid MachineId,
        Guid? ScheduleId,
        DateTime ScheduledDate,
        Guid? TechnicianEmployeeId,
        string? TaskDescription,
        string? Notes,
        decimal? CostImpact);

    [HttpPost("maintenance-work-orders")]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderBody body)
    {
        var result = await Mediator.Send(new CreateMaintenanceWorkOrderCommand(
            body.MachineId, body.ScheduleId, body.ScheduledDate,
            body.TechnicianEmployeeId, body.TaskDescription, body.Notes, body.CostImpact));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    public sealed record CompleteWorkOrderBody(
        DateTime? CompletedAt,
        string? Notes,
        decimal? CostImpact);

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
