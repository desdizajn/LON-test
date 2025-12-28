using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;

namespace LON.Application.WMS.Commands.CreateReceipt;

public record CreateReceiptCommand : ICommand<Result<Guid>>
{
    public DateTime ReceiptDate { get; init; }
    public Guid? PartnerId { get; init; }
    public Guid WarehouseId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public string? ReferenceNumber { get; init; }
    public List<ReceiptLineDto> Lines { get; init; } = new();
}

public record ReceiptLineDto
{
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public string? BatchNumber { get; init; }
    public string? MRN { get; init; }
    public QualityStatus QualityStatus { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
}

public class CreateReceiptCommandHandler : ICommandHandler<CreateReceiptCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateReceiptCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            ReceiptDate = request.ReceiptDate,
            PartnerId = request.PartnerId,
            WarehouseId = request.WarehouseId,
            PurchaseOrderNumber = request.PurchaseOrderNumber,
            ReferenceNumber = request.ReferenceNumber,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        int lineNumber = 1;
        foreach (var lineDto in request.Lines)
        {
            var line = new ReceiptLine
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                BatchNumber = lineDto.BatchNumber,
                MRN = lineDto.MRN,
                QualityStatus = lineDto.QualityStatus,
                ExpiryDate = lineDto.ExpiryDate,
                CustomsDeclarationId = lineDto.CustomsDeclarationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
            receipt.Lines.Add(line);
        }

        receipt.AddDomainEvent(new ReceiptCreatedEvent
        {
            ReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate
        });

        // Context would be accessed via DbContext - this is a placeholder
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(receipt.Id);
    }
}
