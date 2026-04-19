using System.Text.Json;
using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Commands.SetImportDefaults;

public record SetImportDefaultsCommand(Guid SessionId, ImportDefaults Defaults) : ICommand<Result<Guid>>;

public class SetImportDefaultsCommandHandler : ICommandHandler<SetImportDefaultsCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public SetImportDefaultsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(SetImportDefaultsCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session is null)
            return Result<Guid>.Failure($"Import session '{request.SessionId}' not found.");

        // Strip null/empty entries — "clear a default" == remove the key,
        // so the session stays compact and the wizard doesn't have to care
        // about distinguishing absent from explicitly empty.
        var clean = request.Defaults.Values
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        session.DefaultsJson = JsonSerializer.Serialize(new ImportDefaults(clean));
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(session.Id);
    }
}
