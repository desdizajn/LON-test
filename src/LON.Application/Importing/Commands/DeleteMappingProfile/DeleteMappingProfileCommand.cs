using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Commands.DeleteMappingProfile;

public record DeleteMappingProfileCommand(Guid ProfileId) : ICommand<Result<bool>>;

public class DeleteMappingProfileCommandHandler
    : ICommandHandler<DeleteMappingProfileCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteMappingProfileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(
        DeleteMappingProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.ImportMappingProfiles
            .FirstOrDefaultAsync(p => p.Id == request.ProfileId, cancellationToken);
        if (profile is null)
            return Result<bool>.Failure($"Mapping profile '{request.ProfileId}' not found.");

        profile.IsDeleted = true; // soft delete — consistent with rest of codebase
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
