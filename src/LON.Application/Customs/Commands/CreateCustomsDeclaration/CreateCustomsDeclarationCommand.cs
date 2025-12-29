using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Customs.Validation;
using LON.Domain.Entities.Customs;
using LON.Domain.Events;

namespace LON.Application.Customs.Commands.CreateCustomsDeclaration;

public record CreateCustomsDeclarationCommand : ICommand<Result<Guid>>
{
    public string DeclarationNumber { get; init; } = string.Empty;
    public string MRN { get; init; } = string.Empty;
    public DateTime DeclarationDate { get; init; }
    public Guid CustomsProcedureId { get; init; }
    public Guid? PartnerId { get; init; }
    public decimal TotalCustomsValue { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime? DueDate { get; init; }
    public List<DeclarationLineDto> Lines { get; init; } = new();
}

public record DeclarationLineDto
{
    public Guid ItemId { get; init; }
    public string? TariffCode { get; init; }
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public decimal CustomsValue { get; init; }
    public string? CountryOfOrigin { get; init; }
    public decimal DutyRate { get; init; }
    public decimal VATRate { get; init; }
}

public class CreateCustomsDeclarationCommandHandler : ICommandHandler<CreateCustomsDeclarationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDeclarationRuleEngine _ruleEngine;

    public CreateCustomsDeclarationCommandHandler(
        IApplicationDbContext context,
        IDeclarationRuleEngine ruleEngine)
    {
        _context = context;
        _ruleEngine = ruleEngine;
    }

    public async Task<Result<Guid>> Handle(CreateCustomsDeclarationCommand request, CancellationToken cancellationToken)
    {
        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = request.DeclarationNumber,
            MRN = request.MRN,
            DeclarationDate = request.DeclarationDate,
            CustomsProcedureId = request.CustomsProcedureId,
            PartnerId = request.PartnerId,
            TotalCustomsValue = request.TotalCustomsValue,
            Currency = request.Currency,
            DueDate = request.DueDate,
            IsCleared = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        decimal totalDuty = 0;
        decimal totalVAT = 0;
        decimal totalOther = 0;
        int lineNumber = 1;

        foreach (var lineDto in request.Lines)
        {
            var dutyAmount = lineDto.CustomsValue * lineDto.DutyRate / 100;
            var vatAmount = (lineDto.CustomsValue + dutyAmount) * lineDto.VATRate / 100;

            var line = new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                CustomsDeclarationId = declaration.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                TariffCode = lineDto.TariffCode,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                CustomsValue = lineDto.CustomsValue,
                CountryOfOrigin = lineDto.CountryOfOrigin,
                DutyRate = lineDto.DutyRate,
                DutyAmount = dutyAmount,
                VATRate = lineDto.VATRate,
                VATAmount = vatAmount,
                OtherCharges = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            totalDuty += dutyAmount;
            totalVAT += vatAmount;
            declaration.Lines.Add(line);
        }

        declaration.TotalDuty = totalDuty;
        declaration.TotalVAT = totalVAT;
        declaration.TotalOtherCharges = totalOther;
        
        // 🔥 ВАЛИДАЦИЈА со Rule Engine
        var validationResult = await _ruleEngine.ValidateAsync(declaration, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            return Result<Guid>.Failure(
                string.Join("\n", validationResult.GetErrorMessages())
            );
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(declaration.Id);
    }
}
