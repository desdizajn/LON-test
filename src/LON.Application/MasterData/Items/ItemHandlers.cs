using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Queries;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.MasterData.Items;

/// <summary>
/// P6.11 — Items CRUD goes through MediatR. The response shapes (ItemResponse
/// fields) intentionally mirror the pre-P6.10 controller so the frontend
/// itemsApi (which types axios calls as Item / Item[]) stays compatible
/// without schema regeneration.
/// </summary>
public record ItemResponse(
    Guid Id,
    string Code,
    string Name,
    string Description,
    ItemType ItemType,
    Guid UoMId,
    UoMResponseShort? UoM,
    bool IsBatchRequired,
    bool IsMRNRequired,
    string? CountryOfOrigin,
    string? HSCode,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    string? BaseCode,
    string? ColorCode,
    string? SizeCode,
    Guid? ParentItemId);

public record UoMResponseShort(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

internal static class ItemMappers
{
    public static ItemResponse Map(Item item) => new(
        item.Id,
        item.Code,
        item.Name,
        item.Description,
        item.Type,
        item.BaseUoMId,
        item.BaseUoM == null
            ? null
            : new UoMResponseShort(
                item.BaseUoM.Id,
                item.BaseUoM.Code,
                item.BaseUoM.Name,
                item.BaseUoM.Symbol,
                !item.BaseUoM.IsDeleted,
                item.BaseUoM.CreatedAt,
                item.BaseUoM.CreatedBy,
                item.BaseUoM.ModifiedAt,
                item.BaseUoM.ModifiedBy),
        item.IsBatchTracked,
        item.IsMRNTracked,
        item.CountryOfOrigin,
        item.HSCode,
        !item.IsDeleted,
        item.CreatedAt,
        item.CreatedBy,
        item.ModifiedAt,
        item.ModifiedBy,
        item.BaseCode,
        item.ColorCode,
        item.SizeCode,
        item.ParentItemId);
}

// -----------------------------------------------------------------------------
// Query: GetItems (list + optional search on Code/Name)
// -----------------------------------------------------------------------------
public sealed record GetItemsQuery(string? Search = null) : IQuery<List<ItemResponse>>;

public sealed class GetItemsQueryHandler : IQueryHandler<GetItemsQuery, List<ItemResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetItemsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ItemResponse>> Handle(GetItemsQuery request, CancellationToken ct)
    {
        var query = _context.Items
            .Include(i => i.BaseUoM)
            .Where(i => !i.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var s = request.Search;
            query = query.Where(i => i.Code.Contains(s) || i.Name.Contains(s));
        }

        var items = await query.ToListAsync(ct);
        return items.Select(ItemMappers.Map).ToList();
    }
}

// -----------------------------------------------------------------------------
// Query: GetItemById (or null → NotFound at controller boundary)
// -----------------------------------------------------------------------------
public sealed record GetItemByIdQuery(Guid Id) : IQuery<ItemResponse?>;

public sealed class GetItemByIdQueryHandler : IQueryHandler<GetItemByIdQuery, ItemResponse?>
{
    private readonly IApplicationDbContext _context;

    public GetItemByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ItemResponse?> Handle(GetItemByIdQuery request, CancellationToken ct)
    {
        var item = await _context.Items
            .Include(i => i.BaseUoM)
            .Include(i => i.UoMConversions)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);

        return item is null ? null : ItemMappers.Map(item);
    }
}

// -----------------------------------------------------------------------------
// Command: CreateItem
// -----------------------------------------------------------------------------
public sealed record CreateItemCommand(
    string Code,
    string Name,
    string? Description,
    ItemType ItemType,
    Guid UoMId,
    bool IsBatchRequired,
    bool IsMRNRequired,
    string? CountryOfOrigin,
    string? HSCode,
    bool IsActive,
    decimal? StandardCost) : ICommand<ItemResponse>;

public sealed class CreateItemCommandHandler : ICommandHandler<CreateItemCommand, ItemResponse>
{
    private readonly IApplicationDbContext _context;

    public CreateItemCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ItemResponse> Handle(CreateItemCommand request, CancellationToken ct)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Type = request.ItemType,
            IsBatchTracked = request.IsBatchRequired,
            IsMRNTracked = request.IsMRNRequired,
            HSCode = request.HSCode,
            CountryOfOrigin = request.CountryOfOrigin,
            BaseUoMId = request.UoMId,
            StandardCost = request.StandardCost ?? 0m,
            IsDeleted = !request.IsActive
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync(ct);

        // Re-load with BaseUoM for the response mapper.
        var reloaded = await _context.Items
            .Include(i => i.BaseUoM)
            .FirstAsync(i => i.Id == item.Id, ct);
        return ItemMappers.Map(reloaded);
    }
}

// -----------------------------------------------------------------------------
// Command: UpdateItem
// -----------------------------------------------------------------------------
public sealed record UpdateItemCommand(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ItemType ItemType,
    Guid UoMId,
    bool IsBatchRequired,
    bool IsMRNRequired,
    string? CountryOfOrigin,
    string? HSCode,
    bool IsActive,
    decimal? StandardCost) : ICommand<ItemResponse?>;

public sealed class UpdateItemCommandHandler : ICommandHandler<UpdateItemCommand, ItemResponse?>
{
    private readonly IApplicationDbContext _context;

    public UpdateItemCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ItemResponse?> Handle(UpdateItemCommand request, CancellationToken ct)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (item is null) return null;

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description ?? string.Empty;
        item.Type = request.ItemType;
        item.IsBatchTracked = request.IsBatchRequired;
        item.IsMRNTracked = request.IsMRNRequired;
        item.HSCode = request.HSCode;
        item.CountryOfOrigin = request.CountryOfOrigin;
        item.BaseUoMId = request.UoMId;
        item.StandardCost = request.StandardCost ?? item.StandardCost;
        item.IsDeleted = !request.IsActive;

        await _context.SaveChangesAsync(ct);

        var reloaded = await _context.Items
            .Include(i => i.BaseUoM)
            .FirstAsync(i => i.Id == request.Id, ct);
        return ItemMappers.Map(reloaded);
    }
}

// -----------------------------------------------------------------------------
// Command: DeleteItem (soft-delete)
// -----------------------------------------------------------------------------
public sealed record DeleteItemCommand(Guid Id) : ICommand<bool>;

public sealed class DeleteItemCommandHandler : ICommandHandler<DeleteItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteItemCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteItemCommand request, CancellationToken ct)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (item is null) return false;

        item.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
