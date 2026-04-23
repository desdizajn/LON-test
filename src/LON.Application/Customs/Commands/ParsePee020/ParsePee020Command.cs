using System.Xml.Linq;
using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Customs.Commands.CertifyDeclaration;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Commands.ParsePee020;

/// <summary>
/// P15.13.1 — parse a PEE020 response from the customs portal and
/// auto-populate ZaverkaNumber + ZaverkaDate + Status=Cleared on the
/// matching declaration.
///
/// <para>Expected XML shape (envelope + body):</para>
/// <code>
/// &lt;PEE020&gt;
///   &lt;Envelope&gt;...&lt;/Envelope&gt;
///   &lt;PEE020_Body&gt;
///     &lt;MRN&gt;26MKIM10150003D7B3&lt;/MRN&gt;
///     &lt;DeclarationNumber&gt;IMP-001&lt;/DeclarationNumber&gt;
///     &lt;ZaverkaNumber&gt;Z-2026-00345&lt;/ZaverkaNumber&gt;
///     &lt;ZaverkaDate&gt;2026-04-25&lt;/ZaverkaDate&gt;
///     &lt;Status&gt;Cleared&lt;/Status&gt;
///   &lt;/PEE020_Body&gt;
/// &lt;/PEE020&gt;
/// </code>
///
/// <para>Lookup preference:</para>
/// <list type="bullet">
///   <item>If <c>MRN</c> element present, match by <c>CustomsDeclaration.MRN</c>
///         (global unique).</item>
///   <item>Fall back to <c>DeclarationNumber</c> in tenant scope.</item>
/// </list>
///
/// On match: delegates to <see cref="CertifyDeclarationCommand"/> so
/// the same zaverka pipeline (dedupe guard, credit activation,
/// DeclarationCertifiedEvent) runs identically whether entered manually
/// or via the customs portal response.
/// </summary>
public record ParsePee020Command : ICommand<Result<Pee020Parsed>>
{
    public string XmlContent { get; init; } = string.Empty;
}

public record Pee020Parsed(
    Guid DeclarationId,
    string MRN,
    string DeclarationNumber,
    string ZaverkaNumber,
    DateTime ZaverkaDate);

public class ParsePee020CommandHandler : ICommandHandler<ParsePee020Command, Result<Pee020Parsed>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public ParsePee020CommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<Pee020Parsed>> Handle(ParsePee020Command request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.XmlContent))
            return Result<Pee020Parsed>.Failure("PEE020 XML content is empty.");

        XDocument doc;
        try
        {
            doc = XDocument.Parse(request.XmlContent);
        }
        catch (System.Xml.XmlException ex)
        {
            return Result<Pee020Parsed>.Failure($"Invalid PEE020 XML: {ex.Message}");
        }

        // Root can be PEE020 or namespaced; look for the body element first.
        var body = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "PEE020_Body");
        if (body is null)
            return Result<Pee020Parsed>.Failure("PEE020_Body element not found.");

        string? Extract(string localName) =>
            body.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();

        var mrn = Extract("MRN");
        var declarationNumber = Extract("DeclarationNumber");
        var zaverkaNumber = Extract("ZaverkaNumber");
        var zaverkaDateStr = Extract("ZaverkaDate");

        if (string.IsNullOrWhiteSpace(zaverkaNumber))
            return Result<Pee020Parsed>.Failure("ZaverkaNumber missing from PEE020 body.");
        if (string.IsNullOrWhiteSpace(zaverkaDateStr)
            || !DateTime.TryParse(zaverkaDateStr, out var zaverkaDate))
            return Result<Pee020Parsed>.Failure("ZaverkaDate missing or unparseable.");

        // Prefer MRN lookup (global unique); fall back to DeclarationNumber.
        LON.Domain.Entities.Customs.CustomsDeclaration? decl = null;
        if (!string.IsNullOrWhiteSpace(mrn))
        {
            var upper = mrn.ToUpperInvariant();
            decl = await _context.CustomsDeclarations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.MRN == upper && !d.IsDeleted, ct);
        }
        if (decl is null && !string.IsNullOrWhiteSpace(declarationNumber))
        {
            decl = await _context.CustomsDeclarations
                .FirstOrDefaultAsync(d => d.DeclarationNumber == declarationNumber && !d.IsDeleted, ct);
        }
        if (decl is null)
            return Result<Pee020Parsed>.Failure(
                $"No declaration found matching MRN='{mrn}' or DeclarationNumber='{declarationNumber}' in current tenant scope.");

        // Delegate to the existing certify pipeline so dedupe guard +
        // credit-activation + event emission run exactly like manual cert.
        var cert = await _mediator.Send(new CertifyDeclarationCommand(
            decl.Id, zaverkaNumber, zaverkaDate), ct);
        if (!cert.IsSuccess)
            return Result<Pee020Parsed>.Failure(cert.ErrorMessage ?? "Zaverka stamp failed.");

        return Result<Pee020Parsed>.Success(new Pee020Parsed(
            decl.Id, decl.MRN, decl.DeclarationNumber, zaverkaNumber, zaverkaDate));
    }
}
