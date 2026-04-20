using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using LON.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Commands.CertifyDeclaration;

/// <summary>
/// P4.1 — Zaverka / Customs certification.
///
/// The customs inspector stamps the declaration after physical/documentary check.
/// This flips Submitted → Cleared and stamps the legacy-equivalent ZaverkaNumber
/// + ZaverkaDate on the record. ClearedDate is synced to ZaverkaDate for audit.
///
/// Pre-conditions: declaration must exist in tenant scope, not already Cleared or
/// Cancelled. We accept any pre-terminal status (Draft/Registered/Submitted) because
/// in practice legacy TEKSPORT paperwork skips the Submitted step when customs
/// processes inline. Draft → Cleared raises a warning in the audit log.
/// </summary>
public sealed record CertifyDeclarationCommand(
    Guid DeclarationId,
    string ZaverkaNumber,
    DateTime ZaverkaDate) : IRequest<Result<Guid>>;

public sealed class CertifyDeclarationCommandHandler : IRequestHandler<CertifyDeclarationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public CertifyDeclarationCommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<Guid>> Handle(CertifyDeclarationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ZaverkaNumber))
            return Result<Guid>.Failure(ErrorCodes.CertifyZaverkaRequired, "Број на заверка е задолжителен.");

        var decl = await _context.CustomsDeclarations
            .FirstOrDefaultAsync(d => d.Id == request.DeclarationId, cancellationToken);

        if (decl == null)
            return Result<Guid>.Failure(ErrorCodes.CertifyDeclarationNotFound, $"Декларација {request.DeclarationId} не е пронајдена.");

        if (decl.Status == DeclarationStatus.Cleared)
            return Result<Guid>.Failure(ErrorCodes.CertifyAlreadyCertified, "Декларацијата веќе е заверена.");
        if (decl.Status == DeclarationStatus.Cancelled)
            return Result<Guid>.Failure(ErrorCodes.CertifyRemovedDeclaration, "Отстранета декларација не може да се завери.");

        // De-dup guard: another declaration on same tenant cannot already carry this
        // zaverka number (tenant-scoped uniqueness via global query filter).
        var zaverkaTaken = await _context.CustomsDeclarations
            .AnyAsync(d => d.Id != decl.Id && d.ZaverkaNumber == request.ZaverkaNumber, cancellationToken);
        if (zaverkaTaken)
            return Result<Guid>.Failure(ErrorCodes.CertifyZaverkaDuplicate, $"Бројот на заверка '{request.ZaverkaNumber}' веќе постои на друга декларација.");

        decl.ZaverkaNumber = request.ZaverkaNumber.Trim();
        decl.ZaverkaDate = request.ZaverkaDate;
        decl.ClearedDate = request.ZaverkaDate;
        decl.Status = DeclarationStatus.Cleared;
        decl.IsCleared = true;

        decl.AddDomainEvent(new CustomsDeclarationCertifiedEvent(
            decl.Id, decl.MRN, decl.ZaverkaNumber!, decl.ZaverkaDate.Value));

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(decl.Id);
    }
}
