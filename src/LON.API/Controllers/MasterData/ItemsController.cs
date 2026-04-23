using LON.API.MasterData;
using LON.Application.MasterData.Commands.BackfillItemBaseVariants;
using LON.Application.MasterData.Items;
using LON.Application.MasterData.Queries.GetItemImportAttributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers.MasterData;

/// <summary>
/// P6.10 — Items split from the old monolithic MasterDataController.
/// P6.11 — CRUD goes through MediatR handlers in LON.Application.MasterData.Items.
/// URL contract stays identical to the pre-split controller.
/// </summary>
[Route("api/MasterData/items")]
public class ItemsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? search = null,
        [FromQuery] bool? wasteCatalogOnly = null)
    {
        var items = await Mediator.Send(new GetItemsQuery(search, wasteCatalogOnly));
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var item = await Mediator.Send(new GetItemByIdQuery(id));
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem([FromBody] ItemRequest request)
    {
        var item = await Mediator.Send(new CreateItemCommand(
            request.Code,
            request.Name,
            request.Description,
            request.ItemType,
            request.UoMId,
            request.IsBatchRequired,
            request.IsMRNRequired,
            request.CountryOfOrigin,
            request.HSCode,
            request.IsActive,
            request.StandardCost,
            request.PartnerSKU,
            request.PrimaryWasteItemId,
            request.PrimaryWastePercentage,
            request.SecondaryWasteItemId,
            request.SecondaryWastePercentage,
            request.TertiaryWasteItemId,
            request.TertiaryWastePercentage,
            request.ZagubaItemId,
            request.ZagubaPercentage,
            request.WasteTariffCode,
            request.IsWasteCatalog));
        return Ok(item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] ItemRequest request)
    {
        var item = await Mediator.Send(new UpdateItemCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.ItemType,
            request.UoMId,
            request.IsBatchRequired,
            request.IsMRNRequired,
            request.CountryOfOrigin,
            request.HSCode,
            request.IsActive,
            request.StandardCost,
            request.PartnerSKU,
            request.PrimaryWasteItemId,
            request.PrimaryWastePercentage,
            request.SecondaryWasteItemId,
            request.SecondaryWastePercentage,
            request.TertiaryWasteItemId,
            request.TertiaryWastePercentage,
            request.ZagubaItemId,
            request.ZagubaPercentage,
            request.WasteTariffCode,
            request.IsWasteCatalog));
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var deleted = await Mediator.Send(new DeleteItemCommand(id));
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// P6.30 — one-shot backfill of BaseCode/ColorCode/SizeCode/ParentItemId
    /// on legacy items migrated from ELON before KW12 variant model existed.
    /// Admin-only; idempotent (second run is a no-op).
    /// </summary>
    [HttpPost("backfill-base-variants")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> BackfillBaseVariants([FromQuery] bool dryRun = false)
    {
        var result = await Mediator.Send(new BackfillItemBaseVariantsCommand(dryRun));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P6.31 — for a given Item, returns the distinct (TariffCode, CountryOfOrigin,
    /// IsPreferentialOrigin, Supplier, DutyRate, VATRate) tuples across all
    /// active-MRN CustomsDeclarationLines plus the aggregate stock per tuple.
    /// </summary>
    [HttpGet("{id}/import-attributes")]
    public async Task<IActionResult> GetItemImportAttributes(Guid id)
    {
        var result = await Mediator.Send(new GetItemImportAttributesQuery(id));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P5.3.4 — article picker with "A"-suffix variants. Groups matches by
    /// normalised base code so the UI can surface base + A-sibling side by
    /// side for tariff-aware customs line entry.
    /// </summary>
    [HttpGet("article-picker")]
    public async Task<IActionResult> GetArticlePicker(
        [FromQuery] string? query = null,
        [FromQuery] int limit = 20)
    {
        var result = await Mediator.Send(new ArticlePickerQuery(query, limit));
        return Ok(result);
    }
}
