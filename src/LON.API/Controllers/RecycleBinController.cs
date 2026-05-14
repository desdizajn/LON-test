using LON.Application.Customs.ClientOrders;
using LON.Application.RecycleBin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E14 — admin-only recycle bin surface. Lists soft-deleted
/// entities, restores them, or hard-deletes (Administrator role only).
///
/// v1 surfaces only ClientOrder (the canonical hub entity); post-v1
/// expansion adds Partner, Item, Employee, etc.
/// </summary>
[Authorize(Roles = "Administrator")]
[Route("api/admin/recycle-bin")]
public class RecycleBinController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetItems([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await Mediator.Send(new GetRecycleBinQuery { Page = page, PageSize = pageSize });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("client-orders/{id:guid}/restore")]
    public async Task<IActionResult> RestoreClientOrder(Guid id)
    {
        var result = await Mediator.Send(new RestoreClientOrderCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("client-orders/{id:guid}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteClientOrder(Guid id)
    {
        var result = await Mediator.Send(new PermanentDeleteClientOrderCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
