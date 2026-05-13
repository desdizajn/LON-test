using LON.Application.Customs.ClientOrders;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E1 — ClientOrder CRUD endpoints. Hub UI (§E2) consumes these.
/// </summary>
public class ClientOrdersController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? status,
        [FromQuery] Guid? customerPartnerId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool includeCancelled = false)
    {
        var result = await Mediator.Send(new GetClientOrdersQuery
        {
            Status = status,
            CustomerPartnerId = customerPartnerId,
            FromDate = fromDate,
            ToDate = toDate,
            IncludeCancelled = includeCancelled,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetClientOrderByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientOrderCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientOrderCommand command)
    {
        if (command.Id != Guid.Empty && command.Id != id)
            return BadRequest(new { errorMessage = "Route id and body id do not match." });
        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelClientOrderCommand command)
    {
        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E5 — add a <see cref="ClientOrderFinishedGood"/> row to an
    /// existing ClientOrder. Used by the hub's „Внеси готови производи (BOM)" action.
    /// </summary>
    [HttpPost("{id:guid}/finished-goods")]
    public async Task<IActionResult> AddFinishedGood(Guid id, [FromBody] AddClientOrderFinishedGoodCommand command)
    {
        var effective = command with { ClientOrderId = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
