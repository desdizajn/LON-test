using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Production.Commands.IssueAllMaterials;
using LON.Application.Production.Commands.ReleaseProductionOrder;
using LON.Application.WMS.Commands.MoveBatchAcrossStages;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.QuickEntry;

/// <summary>
/// P5.2.8 — single-line command bar for power users.
///
/// Supported commands (case-insensitive):
///
/// - <c>issue &lt;po-number&gt;</c> — bulk-issues all remaining materials of the
///   referenced production order (delegates to <see cref="IssueAllMaterialsCommand"/>).
/// - <c>release &lt;po-number&gt;</c> — releases Draft → Released.
/// - <c>move &lt;batch&gt; &lt;stage&gt;</c> — moves every InventoryBalance
///   carrying the batch to the given <see cref="LocationType"/>. Stage can be
///   either the enum name (production / shipping / …) or the integer value.
/// - <c>help</c> — lists commands.
///
/// The parser is deliberately simple: whitespace-split, first token is the
/// verb, rest are positional args. No quoting; if a value contains spaces,
/// use the regular form instead. Tokens are trimmed and the verb is
/// lower-cased; args preserve case so batch numbers with a lowercase suffix
/// still match.
/// </summary>
public sealed record QuickEntryCommand(string Input) : ICommand<Result<QuickEntryResult>>;

public sealed record QuickEntryResult(
    string Verb,
    string Outcome,
    object? Payload = null);

public class QuickEntryCommandHandler
    : ICommandHandler<QuickEntryCommand, Result<QuickEntryResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public QuickEntryCommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<QuickEntryResult>> Handle(QuickEntryCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            return Result<QuickEntryResult>.Failure("Empty command. Type 'help' to see available verbs.");

        var tokens = request.Input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return Result<QuickEntryResult>.Failure("Empty command.");

        var verb = tokens[0].ToLowerInvariant();
        var args = tokens.Skip(1).ToArray();

        return verb switch
        {
            "help" => Result<QuickEntryResult>.Success(new QuickEntryResult(
                "help",
                "Available: issue <po>, release <po>, move <batch> <stage>, help")),

            "issue" => await HandleIssue(args, ct),
            "release" => await HandleRelease(args, ct),
            "move" => await HandleMove(args, ct),

            _ => Result<QuickEntryResult>.Failure(
                $"Unknown verb '{verb}'. Type 'help' to see supported commands.")
        };
    }

    private async Task<Result<QuickEntryResult>> HandleIssue(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return Result<QuickEntryResult>.Failure("Usage: issue <po-number>");

        var poNumber = args[0];
        var po = await _context.ProductionOrders
            .FirstOrDefaultAsync(p => p.OrderNumber == poNumber, ct);
        if (po is null)
            return Result<QuickEntryResult>.Failure($"ProductionOrder '{poNumber}' not found.");

        var inner = await _mediator.Send(new IssueAllMaterialsCommand(po.Id, DateTime.UtcNow), ct);
        if (!inner.IsSuccess)
            return Result<QuickEntryResult>.Failure(inner.ErrorMessage ?? "issue failed");

        return Result<QuickEntryResult>.Success(new QuickEntryResult(
            "issue",
            $"Bulk issue completed for PO '{poNumber}'.",
            new { productionOrderId = po.Id, issueId = inner.Data }));
    }

    private async Task<Result<QuickEntryResult>> HandleRelease(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return Result<QuickEntryResult>.Failure("Usage: release <po-number>");

        var poNumber = args[0];
        var po = await _context.ProductionOrders
            .FirstOrDefaultAsync(p => p.OrderNumber == poNumber, ct);
        if (po is null)
            return Result<QuickEntryResult>.Failure($"ProductionOrder '{poNumber}' not found.");

        var inner = await _mediator.Send(new ReleaseProductionOrderCommand(po.Id), ct);
        if (!inner.IsSuccess)
            return Result<QuickEntryResult>.Failure(inner.ErrorMessage ?? "release failed");

        return Result<QuickEntryResult>.Success(new QuickEntryResult(
            "release",
            $"PO '{poNumber}' released.",
            new { productionOrderId = po.Id }));
    }

    private async Task<Result<QuickEntryResult>> HandleMove(string[] args, CancellationToken ct)
    {
        if (args.Length < 2)
            return Result<QuickEntryResult>.Failure("Usage: move <batch> <stage>");

        var batch = args[0];
        var stageArg = args[1];

        if (!TryParseStage(stageArg, out var stage))
            return Result<QuickEntryResult>.Failure(
                $"Unknown stage '{stageArg}'. Accepted: receiving, storage, picking, production, shipping, quarantine, blocked (or integer 1-7).");

        var inner = await _mediator.Send(
            new MoveBatchAcrossStagesCommand(batch, stage, Reason: "quick-entry"),
            ct);
        if (!inner.IsSuccess)
            return Result<QuickEntryResult>.Failure(inner.ErrorMessage ?? "move failed");

        return Result<QuickEntryResult>.Success(new QuickEntryResult(
            "move",
            $"Moved {inner.Data!.BalancesMoved} balance(s) / {inner.Data.TotalQuantityMoved} units of batch '{batch}' to {stage}.",
            inner.Data));
    }

    private static bool TryParseStage(string input, out LocationType stage)
    {
        if (int.TryParse(input, out var asInt) && Enum.IsDefined(typeof(LocationType), asInt))
        {
            stage = (LocationType)asInt;
            return true;
        }
        if (Enum.TryParse<LocationType>(input, ignoreCase: true, out var asName))
        {
            stage = asName;
            return true;
        }
        stage = default;
        return false;
    }
}
