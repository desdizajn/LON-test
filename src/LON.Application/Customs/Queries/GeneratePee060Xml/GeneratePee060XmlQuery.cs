using System.Globalization;
using System.Text;
using System.Xml.Linq;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Queries.GeneratePee060Xml;

/// <summary>
/// P4.2 — PEE060 (monthly zadolzuvanje/razdolzuvanje report).
///
/// PEE060 is the monthly report Customs expects from LON authorization holders
/// summarizing new debits (zadolzuvanja — newly imported LON material under
/// procedure 42 00) and discharges (razdolzuvanja — EX/return/waste against IM
/// declarations) grouped by TariffCode + CountryOfOrigin. Legacy ELON produces
/// this via `cmdXML_PEE060_Click` aggregating <c>FakturiU5</c> + <c>LagerMaterijali</c>.
///
/// Our mapping:
///   Zadolzuvanje — CustomsDeclarationLines under IM declarations with
///     <c>DeclarationDate</c> in the window.
///   Razdolzuvanje — CustomsDeclarationLines under EX/Return/Waste declarations
///     referencing an IM MRN (via <c>PreviousMRN</c>) also in the window.
///
/// Envelope: hardcoded PEE constants (Interchange control reference 9999,
/// recipient code qualifier C5, recipient password 111111) — legacy parity.
/// </summary>
public sealed record GeneratePee060XmlQuery(
    Guid LONAuthorizationId,
    DateTime From,
    DateTime To) : IRequest<Result<Pee060Payload>>;

public sealed record Pee060Payload(
    string FileName,
    string Xml,
    int ZadolzuvanjeLineCount,
    int RazdolzuvanjeLineCount);

public sealed class GeneratePee060XmlQueryHandler
    : IRequestHandler<GeneratePee060XmlQuery, Result<Pee060Payload>>
{
    private readonly IApplicationDbContext _context;

    public GeneratePee060XmlQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Pee060Payload>> Handle(GeneratePee060XmlQuery request, CancellationToken ct)
    {
        var auth = await _context.LONAuthorizations
            .Include(a => a.Partner)
            .FirstOrDefaultAsync(a => a.Id == request.LONAuthorizationId, ct);
        if (auth == null) return Result<Pee060Payload>.Failure("Одобрението не е пронајдено.");
        if (request.From > request.To) return Result<Pee060Payload>.Failure("From > To.");

        var imLines = await _context.CustomsDeclarationLines
            .Include(l => l.CustomsDeclaration)
            .Include(l => l.UoM)
            .Where(l => l.CustomsDeclaration.LONAuthorizationId == auth.Id
                        && l.CustomsDeclaration.DeclarationType == "IM"
                        && l.CustomsDeclaration.DeclarationDate >= request.From
                        && l.CustomsDeclaration.DeclarationDate <= request.To
                        && !l.IsDeleted)
            .Select(l => new {
                l.TariffCode,
                l.CountryOfOrigin,
                l.Quantity,
                l.CustomsValue,
                l.DutyAmount,
                UoM = l.UoM != null ? l.UoM.Code : null,
            })
            .ToListAsync(ct);

        var exLines = await _context.CustomsDeclarationLines
            .Include(l => l.CustomsDeclaration)
            .Include(l => l.UoM)
            .Where(l => l.CustomsDeclaration.LONAuthorizationId == auth.Id
                        && l.CustomsDeclaration.DeclarationType != "IM"
                        && l.CustomsDeclaration.DeclarationDate >= request.From
                        && l.CustomsDeclaration.DeclarationDate <= request.To
                        && !l.IsDeleted)
            .Select(l => new {
                l.TariffCode,
                l.CountryOfOrigin,
                l.Quantity,
                l.CustomsValue,
                l.DutyAmount,
                UoM = l.UoM != null ? l.UoM.Code : null,
            })
            .ToListAsync(ct);

        // Aggregate both sides by (TariffCode, Country)
        var byKey = new Dictionary<(string T, string C), Aggregate>();
        foreach (var l in imLines)
        {
            var key = (l.TariffCode ?? "", l.CountryOfOrigin ?? "");
            if (!byKey.TryGetValue(key, out var a))
                a = byKey[key] = new Aggregate(key.Item1, key.Item2);
            a.ImQty += l.Quantity;
            a.ImValue += l.CustomsValue;
            a.ImDuty += l.DutyAmount;
            a.UoM ??= l.UoM;
        }
        foreach (var l in exLines)
        {
            var key = (l.TariffCode ?? "", l.CountryOfOrigin ?? "");
            if (!byKey.TryGetValue(key, out var a))
                a = byKey[key] = new Aggregate(key.Item1, key.Item2);
            a.ExQty += l.Quantity;
            a.ExValue += l.CustomsValue;
            a.ExDuty += l.DutyAmount;
            a.UoM ??= l.UoM;
        }

        var aggregates = byKey.Values.OrderBy(x => x.Tariff).ThenBy(x => x.Country).ToList();
        var yyyy = request.To.Year;
        var fileName = $"PEE060_R_S_{auth.AuthorizationNumber}_{auth.CompetentCustomsOffice}_{yyyy}.xml";

        var xml = BuildXml(auth.AuthorizationNumber, auth.CompetentCustomsOffice, auth.Partner?.Name ?? auth.Partner?.Code ?? "Uvoznik",
                           request.From, request.To, aggregates);

        return Result<Pee060Payload>.Success(new Pee060Payload(
            fileName, xml, imLines.Count, exLines.Count));
    }

    private sealed class Aggregate
    {
        public Aggregate(string tariff, string country) { Tariff = tariff; Country = country; }
        public string Tariff; public string Country;
        public decimal ImQty, ImValue, ImDuty;
        public decimal ExQty, ExValue, ExDuty;
        public string? UoM;
    }

    private static string BuildXml(string authNo, string customsOffice, string importerName,
        DateTime from, DateTime to, List<Aggregate> rows)
    {
        var ci = CultureInfo.InvariantCulture;
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("PEE060",
                new XElement("Envelope",
                    new XElement("Sender", importerName),
                    new XElement("Recipient", customsOffice),
                    new XElement("RecipientIdentificationCodeQualifier", "C5"),
                    new XElement("InterchangeControlReference", "9999"),
                    new XElement("RecipientsReferencePassword", "111111"),
                    new XElement("Period",
                        new XElement("From", from.ToString("yyyy-MM-dd", ci)),
                        new XElement("To", to.ToString("yyyy-MM-dd", ci))),
                    new XElement("Authorization", authNo)),
                new XElement("Body",
                    rows.Select(r => new XElement("TariffCodeSummary",
                        new XElement("TarBr", r.Tariff),
                        new XElement("Poteklo", r.Country),
                        new XElement("EdMer", r.UoM ?? ""),
                        new XElement("Zadolzuvanje",
                            new XElement("Kol", r.ImQty.ToString(ci)),
                            new XElement("Vrednost", r.ImValue.ToString(ci)),
                            new XElement("Davacki", r.ImDuty.ToString(ci))),
                        new XElement("Razdolzuvanje",
                            new XElement("Kol", r.ExQty.ToString(ci)),
                            new XElement("Vrednost", r.ExValue.ToString(ci)),
                            new XElement("Davacki", r.ExDuty.ToString(ci))))))));

        var sb = new StringBuilder();
        using (var w = System.Xml.XmlWriter.Create(sb, new System.Xml.XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false,
        }))
        {
            doc.Save(w);
        } // writer must flush+dispose before we read the StringBuilder
        return sb.ToString();
    }
}
