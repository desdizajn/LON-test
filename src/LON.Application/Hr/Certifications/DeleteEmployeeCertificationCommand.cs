using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Hr.Certifications;

public sealed record DeleteEmployeeCertificationCommand(Guid Id) : ICommand<Result<bool>>;

public class DeleteEmployeeCertificationCommandHandler
    : ICommandHandler<DeleteEmployeeCertificationCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteEmployeeCertificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteEmployeeCertificationCommand request, CancellationToken ct)
    {
        var entity = await _context.EmployeeCertifications
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (entity is null)
            return Result<bool>.Failure($"EmployeeCertification '{request.Id}' not found.");

        entity.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
