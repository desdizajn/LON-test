using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Enums;
using LON.Domain.Events;

namespace LON.Application.Guarantee.Commands.CreditGuarantee;

public record CreditGuaranteeCommand : ICommand<Result<Guid>>
{
    public Guid GuaranteeAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? MRN { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
    public Guid? RelatedDebitEntryId { get; init; }
}

public class CreditGuaranteeCommandHandler : ICommandHandler<CreditGuaranteeCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreditGuaranteeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreditGuaranteeCommand request, CancellationToken cancellationToken)
    {
        var entry = new GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = request.GuaranteeAccountId,
            EntryDate = DateTime.UtcNow,
            EntryType = GuaranteeEntryType.Credit,
            Amount = request.Amount,
            Description = request.Description,
            MRN = request.MRN,
            CustomsDeclarationId = request.CustomsDeclarationId,
            ActualReleaseDate = DateTime.UtcNow,
            IsReleased = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        entry.AddDomainEvent(new GuaranteeCreditedEvent
        {
            GuaranteeAccountId = request.GuaranteeAccountId,
            Amount = request.Amount,
            MRN = request.MRN,
            CustomsDeclarationId = request.CustomsDeclarationId
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(entry.Id);
    }
}
