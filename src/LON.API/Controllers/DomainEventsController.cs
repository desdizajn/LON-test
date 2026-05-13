using LON.Application.DomainEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E11 — read surface over DomainEventLogs. Admin-only so the
/// payload (which can carry partner/customer ids and PII-adjacent metadata)
/// isn't visible to regular operators.
/// </summary>
[Authorize(Roles = "Administrator")]
[Route("api/admin/domain-events")]
public class DomainEventsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? eventType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await Mediator.Send(new GetDomainEventLogQuery
        {
            EventType = eventType,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
