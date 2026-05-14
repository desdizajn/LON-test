using LON.Application.Finance.FxRates;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E16 — admin CRUD over FX rates + the point-in-time lookup
/// used by valuation paths. Lives under /api/Finance/fx-rates to match the
/// rest of the Finance surface.
/// </summary>
[Route("api/Finance/fx-rates")]
public class FxRatesController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var result = await Mediator.Send(new GetFxRatesQuery
        {
            FromCurrency = from,
            ToCurrency = to,
            From = dateFrom,
            To = dateTo,
            Page = page,
            PageSize = pageSize,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFxRateCommand cmd)
    {
        var result = await Mediator.Send(cmd);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFxRateCommand cmd)
    {
        var canonical = cmd with { Id = id };
        var result = await Mediator.Send(canonical);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await Mediator.Send(new DeleteFxRateCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("effective")]
    public async Task<IActionResult> GetEffective(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] DateTime? asOf = null)
    {
        var ts = asOf ?? DateTime.UtcNow;
        var result = await Mediator.Send(new GetEffectiveRateQuery(from, to, ts));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
