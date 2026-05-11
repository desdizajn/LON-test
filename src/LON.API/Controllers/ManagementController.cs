using LON.Application.Management;
using LON.Application.Management.Risks;
using LON.Domain.Entities.Management;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P13.1 / P13.3 / P13.5 — management KPIs: on-time delivery, by-customer
/// rollup, exception alerts feed. Lives under <c>/api/Management</c>.
/// </summary>
[Route("api/Management")]
public class ManagementController : BaseController
{
    /// <summary>P13.1 — on-time delivery per shipment + per-customer rollup.</summary>
    [HttpGet("on-time")]
    public async Task<IActionResult> GetOnTime([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetOnTimeReportQuery(from, to));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>P13.3 — production + shipment + invoice rollup per customer.</summary>
    [HttpGet("by-customer")]
    public async Task<IActionResult> GetByCustomer([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetByCustomerReportQuery(from, to));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>P13.5 — aggregated exception alerts across MRN expiry,
    /// overdue invoices, material shortage, at-risk POs, LON-auth expiry.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var result = await Mediator.Send(new GetAlertsQuery());
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ─────────────────────────── P16.C1 — risks ──────────────────────────
    // Backs both pages/Management/OpenRisks.tsx (kind=Risk) and
    // pages/Management/Escalations.tsx (kind=Escalation). Tenant isolation
    // is enforced by the global EF query filter.

    /// <summary>P16.C1 — list risks/escalations (optional kind filter).</summary>
    [HttpGet("risks")]
    public async Task<IActionResult> GetRisks([FromQuery] RiskKind? kind)
    {
        var result = await Mediator.Send(new GetRiskRegisterItemsQuery(kind));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>P16.C1 — get one risk/escalation by id.</summary>
    [HttpGet("risks/{id:guid}")]
    public async Task<IActionResult> GetRisk(Guid id)
    {
        var result = await Mediator.Send(new GetRiskRegisterItemByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    /// <summary>P16.C1 — create risk/escalation.</summary>
    [HttpPost("risks")]
    public async Task<IActionResult> CreateRisk([FromBody] CreateRiskRegisterItemCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>P16.C1 — update risk/escalation.</summary>
    [HttpPut("risks/{id:guid}")]
    public async Task<IActionResult> UpdateRisk(Guid id, [FromBody] UpdateRiskRegisterItemCommand command)
    {
        if (command.Id != id) command = command with { Id = id };
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>P16.C1 — soft-delete risk/escalation.</summary>
    [HttpDelete("risks/{id:guid}")]
    public async Task<IActionResult> DeleteRisk(Guid id)
    {
        var result = await Mediator.Send(new DeleteRiskRegisterItemCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
