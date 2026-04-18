using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Commands.UpdateCustomsDeclaration;

/// <summary>
/// Updates a <c>Draft</c> declaration in place. Post-<c>Registered</c> edits
/// are rejected — customs compliance demands an amendment workflow (not yet
/// implemented) rather than silent mutation of an already-filed declaration.
///
/// <para>
/// Scope is deliberately narrow: header text fields + partner/due date only.
/// Lines, MRN, LONAuthorization, procedure, and currency are frozen once the
/// declaration is created (any of those changing would require re-running
/// duty calculation, MRN regeneration, and guarantee re-debit — and that is
/// exactly the amendment flow we push to a later phase).
/// </para>
/// </summary>
public record UpdateCustomsDeclarationCommand : ICommand<Result<Guid>>
{
    public Guid Id { get; init; }
    public string? DeclarationNumber { get; init; }
    public Guid? PartnerId { get; init; }
    public DateTime? DueDate { get; init; }
    public string? SenderName { get; init; }
    public string? SenderAddress { get; init; }
    public string? SenderCountry { get; init; }
    public string? CountryOfDispatch { get; init; }
    public string? CountryOfDestination { get; init; }
    public string? SpecialRemarks { get; init; }
    public string? Notes { get; init; }
}

public class UpdateCustomsDeclarationCommandHandler : ICommandHandler<UpdateCustomsDeclarationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateCustomsDeclarationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(UpdateCustomsDeclarationCommand request, CancellationToken cancellationToken)
    {
        var declaration = await _context.CustomsDeclarations
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (declaration is null)
            return Result<Guid>.Failure($"Declaration '{request.Id}' does not exist.");

        if (declaration.Status != DeclarationStatus.Draft)
        {
            return Result<Guid>.Failure(
                $"Declaration '{declaration.DeclarationNumber}' is in status '{declaration.Status}' and cannot be edited. " +
                "Customs rules require an amendment workflow for declarations past Draft — this is a deliberate block " +
                "to prevent silent modification of filed declarations.");
        }

        if (request.DeclarationNumber is not null)
            declaration.DeclarationNumber = request.DeclarationNumber.Trim();
        if (request.PartnerId.HasValue)
            declaration.PartnerId = request.PartnerId;
        if (request.DueDate.HasValue)
            declaration.DueDate = request.DueDate;
        if (request.SenderName is not null)
            declaration.SenderName = request.SenderName;
        if (request.SenderAddress is not null)
            declaration.SenderAddress = request.SenderAddress;
        if (request.SenderCountry is not null)
            declaration.SenderCountry = request.SenderCountry;
        if (request.CountryOfDispatch is not null)
            declaration.CountryOfDispatch = request.CountryOfDispatch;
        if (request.CountryOfDestination is not null)
            declaration.CountryOfDestination = request.CountryOfDestination;
        if (request.SpecialRemarks is not null)
            declaration.SpecialRemarks = request.SpecialRemarks;
        if (request.Notes is not null)
            declaration.Notes = request.Notes;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(declaration.Id);
    }
}
