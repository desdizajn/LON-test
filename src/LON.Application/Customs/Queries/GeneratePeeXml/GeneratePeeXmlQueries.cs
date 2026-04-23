using System.Globalization;
using System.Text;
using System.Xml.Linq;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Common.Queries;
using LON.Application.Customs.Queries.GetDeclarationNaim;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Queries.GeneratePeeXml;

/// <summary>
/// P15.12 / P15.13 / P15.14 / P15.15 — legacy PEE family of XML envelopes
/// for Macedonian customs. All four are generated from a single
/// <see cref="CustomsDeclaration"/> (identified by id) and differ only
/// in the envelope metadata + which sections appear in the body.
///
/// <para>Legacy layout (ELON <c>PEE_XML</c> metadata-driven table):</para>
/// <list type="bullet">
///   <item>PEE010 — IM submission envelope (pre-clearance).</item>
///   <item>PEE020 — IM clearance response parser (INBOUND — placeholder stub).</item>
///   <item>PEE040 — Waste declaration envelope.</item>
///   <item>PEE050 — EX submission envelope (re-export of LON goods).</item>
/// </list>
///
/// Envelope constants (sender code qualifier, interchange control reference,
/// recipient password) match legacy hardcoded values `9999` / `C5` / `111111`
/// so the customs portal accepts the file without re-configuration.
///
/// The body structure is pragmatic — the legacy ELON XML tree is driven by
/// the PEE_XML metadata table; we generate a faithful-enough subset that
/// carries the fields customs requires (Box 1, 2, 8, 14, 15, 17, 22, 23,
/// 31, 33, 34, 35, 37, 38, 41, 44, 46, 47) grouped into NaimU5 rollups so
/// the output reads like the legacy report.
/// </summary>
public sealed record GeneratePeeXmlQuery(Guid DeclarationId, string Envelope)
    : IQuery<Result<PeeXmlPayload>>;

public sealed record PeeXmlPayload(
    string FileName,
    string Xml,
    int NaimCount,
    int LineCount);

public sealed class GeneratePeeXmlQueryHandler
    : IQueryHandler<GeneratePeeXmlQuery, Result<PeeXmlPayload>>
{
    private static readonly HashSet<string> KnownEnvelopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PEE010", "PEE020", "PEE040", "PEE050"
    };

    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GeneratePeeXmlQueryHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<PeeXmlPayload>> Handle(GeneratePeeXmlQuery request, CancellationToken ct)
    {
        if (!KnownEnvelopes.Contains(request.Envelope))
            return Result<PeeXmlPayload>.Failure(
                $"Envelope '{request.Envelope}' not supported. Use PEE010 / PEE020 / PEE040 / PEE050.");

        var decl = await _context.CustomsDeclarations
            .Include(d => d.Partner)
            .Include(d => d.CustomsProcedure)
            .Include(d => d.LONAuthorization)
            .FirstOrDefaultAsync(d => d.Id == request.DeclarationId, ct);
        if (decl is null)
            return Result<PeeXmlPayload>.Failure($"Declaration '{request.DeclarationId}' not found.");

        // Cross-check envelope vs declaration type to avoid nonsensical output.
        var mismatch = (request.Envelope.ToUpperInvariant(), decl.DeclarationType) switch
        {
            ("PEE010", "IM") => null,
            ("PEE010", _) => "PEE010 envelope requires DeclarationType=IM.",
            ("PEE050", "EX") => null,
            ("PEE050", _) => "PEE050 envelope requires DeclarationType=EX.",
            ("PEE040", "Waste") => null,
            ("PEE040", _) => "PEE040 envelope requires DeclarationType=Waste.",
            ("PEE020", _) => null, // PEE020 is inbound — envelope is always valid stub
            _ => null
        };
        if (mismatch is not null)
            return Result<PeeXmlPayload>.Failure(mismatch);

        var naimRows = await _mediator.Send(new GetDeclarationNaimQuery(decl.Id), ct);

        var yyyy = decl.DeclarationDate.Year;
        var fileName = $"{request.Envelope}_{decl.DeclarationNumber}_{yyyy}.xml".Replace(" ", "_");

        var xml = BuildXml(request.Envelope.ToUpperInvariant(), decl, naimRows);

        var lineCount = await _context.CustomsDeclarationLines
            .CountAsync(l => l.CustomsDeclarationId == decl.Id && !l.IsDeleted, ct);

        return Result<PeeXmlPayload>.Success(new PeeXmlPayload(
            fileName, xml, naimRows.Count, lineCount));
    }

    private static string BuildXml(string envelope, LON.Domain.Entities.Customs.CustomsDeclaration decl, List<NaimRow> naim)
    {
        var ns = XNamespace.None;
        var senderName = decl.LONAuthorization?.Partner?.Name ?? decl.SenderName ?? "Uvoznik";
        var recipientCode = decl.LONAuthorization?.CompetentCustomsOffice ?? decl.CustomsProcedure?.Code ?? "MK003001";

        var body = new XElement(ns + envelope + "_Body",
            new XElement("MRN", decl.MRN),
            new XElement("DeclarationNumber", decl.DeclarationNumber),
            new XElement("DeclarationType", decl.DeclarationType),
            new XElement("ProcedureCode", decl.ProcedureCode ?? ""),
            new XElement("PreviousProcedureCode", decl.PreviousProcedureCode ?? "00"),
            new XElement("DeclarationDate", decl.DeclarationDate.ToString("yyyy-MM-dd")),
            new XElement("Currency", decl.Currency),
            new XElement("TotalCustomsValue", decl.TotalCustomsValue.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("TotalDuty", decl.TotalDuty.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("TotalVAT", decl.TotalVAT.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("SenderName", decl.SenderName ?? ""),
            new XElement("SenderCountry", decl.SenderCountry ?? ""),
            new XElement("ReceiverName", decl.ReceiverName ?? senderName),
            new XElement("CountryOfDispatch", decl.CountryOfDispatch ?? ""),
            new XElement("CountryOfDestination", decl.CountryOfDestination ?? ""),
            new XElement("LONAuthorizationNumber", decl.LONAuthorization?.AuthorizationNumber ?? ""),
            new XElement("Naimenovanija",
                naim.Select(n => new XElement("Naim",
                    new XAttribute("num", n.NaimNumber),
                    new XElement("TariffCode", n.TariffCode ?? ""),
                    new XElement("UoM", n.UoMCode),
                    new XElement("CountryOfOrigin", n.CountryOfOrigin ?? ""),
                    new XElement("Quantity", n.TotalQuantity.ToString("0.0000", CultureInfo.InvariantCulture)),
                    new XElement("CustomsValue", n.TotalCustomsValue.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("GrossWeight", (n.TotalGrossWeight ?? 0m).ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("NetWeight", (n.TotalNetWeight ?? 0m).ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("DutyRate", n.WeightedAverageDutyRate.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("DutyAmount", n.TotalDutyAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("VATRate", n.WeightedAverageVATRate.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("VATAmount", n.TotalVATAmount.ToString("0.00", CultureInfo.InvariantCulture))
                ))));

        // PEE020 is inbound — include a stub <ParseInstructions/> element so the
        // file round-trips through the customs portal's validation (real clearance
        // data is written back to the declaration via a separate parser).
        if (envelope == "PEE020")
            body.Add(new XElement("ParseInstructions",
                new XAttribute("note", "Inbound response stub — populate ZaverkaNumber/Date on receipt")));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(envelope,
                new XElement("Envelope",
                    new XElement("InterchangeControlReference", "9999"),
                    new XElement("Sender", senderName),
                    new XElement("SenderCodeQualifier", "C5"),
                    new XElement("Recipient", recipientCode),
                    new XElement("RecipientPassword", "111111"),
                    new XElement("GeneratedAt", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))),
                body));

        using var sw = new StringWriter();
        doc.Save(sw, SaveOptions.None);
        return sw.ToString();
    }
}
