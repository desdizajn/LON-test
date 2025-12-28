using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Enums;
using LON.Domain.Events;

namespace LON.Application.Guarantee.Commands.DebitGuarantee;

public record DebitGuaranteeCommand : ICommand<Result<Guid>>
{
    public Guid GuaranteeAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? MRN { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
    public DateTime? ExpectedReleaseDate { get; init; }
}

public class DebitGuaranteeCommandHandler : ICommandHandler<DebitGuaranteeCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public DebitGuaranteeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(DebitGuaranteeCommand request, CancellationToken cancellationToken)
    {
        var entry = new GuaranteeLedgerEntry
        {
            Id = Guid.NewGuid(),
            GuaranteeAccountId = request.GuaranteeAccountId,
            EntryDate = DateTime.UtcNow,
            EntryType = GuaranteeEntryType.Debit,
            Amount = request.Amount,
            Description = request.Description,
            MRN = request.MRN,
            CustomsDeclarationId = request.CustomsDeclarationId,
            ExpectedReleaseDate = request.ExpectedReleaseDate,
            IsReleased = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        entry.AddDomainEvent(new GuaranteeDebitedEvent
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
