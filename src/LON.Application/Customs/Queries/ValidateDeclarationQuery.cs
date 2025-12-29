using LON.Application.Common.Commands;
using LON.Application.Common.Models;
using LON.Application.Customs.Validation;
using MediatR;

namespace LON.Application.Customs.Queries;

/// <summary>
/// Query за валидација на декларација пред креирање
/// </summary>
public record ValidateDeclarationQuery : IRequest<Result<DeclarationValidationResult>>
{
    public string DeclarationType { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public string? Receiver { get; init; }
    public string TariffCode { get; init; } = string.Empty;
    public string ProcedureCode { get; init; } = string.Empty;
    public decimal? DutyRate { get; init; }
    public string? Documents { get; init; }
    // Додај останати полиња по потреба
}

/// <summary>
/// Handler за ValidateDeclarationQuery
/// </summary>
public class ValidateDeclarationQueryHandler : IRequestHandler<ValidateDeclarationQuery, Result<DeclarationValidationResult>>
{
    private readonly IDeclarationRuleEngine _ruleEngine;
    
    public ValidateDeclarationQueryHandler(IDeclarationRuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }
    
    public async Task<Result<DeclarationValidationResult>> Handle(
        ValidateDeclarationQuery request, 
        CancellationToken cancellationToken)
    {
        // Креирај привремена декларација за валидација
        var declaration = new Domain.Entities.Customs.CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationType = request.DeclarationType,
            SenderName = request.Sender,
            ReceiverName = request.Receiver,
            ProcedureCode = request.ProcedureCode,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Validation"
        };
        
        // Додај линија за валидација на TariffCode
        if (!string.IsNullOrEmpty(request.TariffCode))
        {
            declaration.Lines.Add(new Domain.Entities.Customs.CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                CustomsDeclarationId = declaration.Id,
                LineNumber = 1,
                TariffCode = request.TariffCode,
                ItemId = Guid.Empty, // Mock за валидација
                UoMId = Guid.Empty, // Mock за валидација
                Quantity = 1,
                CustomsValue = 0,
                DutyRate = request.DutyRate ?? 0,
                VATRate = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Validation"
            });
        }
        
        var validationResult = await _ruleEngine.ValidateAsync(declaration, cancellationToken);
        
        return Result<DeclarationValidationResult>.Success(validationResult);
    }
}
