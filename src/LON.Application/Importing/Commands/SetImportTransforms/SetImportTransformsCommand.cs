using System.Text.Json;
using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Commands.SetImportTransforms;

public record SetImportTransformsCommand(Guid SessionId, ImportTransforms Transforms) : ICommand<Result<Guid>>;

public class SetImportTransformsCommandHandler : ICommandHandler<SetImportTransformsCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public SetImportTransformsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        SetImportTransformsCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session is null)
            return Result<Guid>.Failure($"Import session '{request.SessionId}' not found.");

        // Validate against uploaded headers so the user can't pin a transform
        // to a column that doesn't exist.
        var headers = JsonSerializer.Deserialize<List<string>>(session.HeadersJson) ?? new List<string>();
        var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        foreach (var t in request.Transforms.Columns)
        {
            if (!headerSet.Contains(t.SourceHeader))
                return Result<Guid>.Failure($"Transform references unknown column '{t.SourceHeader}'.");
        }

        session.TransformsJson = JsonSerializer.Serialize(request.Transforms);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(session.Id);
    }
}
