using LON.Application.FinishedGoods;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P9.1 / P9.6 — Finished Goods simple queries: awaiting-pack, packaging-stock.
/// P9.3 ready-to-ship is served by the existing ShipmentsByStatus FE
/// component (no dedicated endpoint needed — reuses GET /WMS/shipments).
/// </summary>
[Route("api/FinishedGoods")]
public class FinishedGoodsController : BaseController
{
    [HttpGet("awaiting-pack")]
    public async Task<IActionResult> GetAwaitingPack()
    {
        var result = await Mediator.Send(new GetAwaitingPackQuery());
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("packaging-stock")]
    public async Task<IActionResult> GetPackagingStock()
    {
        var result = await Mediator.Send(new GetPackagingStockQuery());
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
