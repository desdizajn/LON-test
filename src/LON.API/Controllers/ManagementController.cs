using LON.Application.Management;
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
}
