using System.Xml.Linq;
using LON.Application.Common.Importing;
using LON.Domain.Enums;

namespace LON.Infrastructure.Services.Importing;

/// <summary>
/// Generic record-set XML parser. Looks at the root, finds its most common
/// repeated child element name, and treats each such child as a row. Each
/// row's data is drawn first from attributes, then from direct child elements
/// (flattened to their inner text). Headers = union of attribute + child names
/// observed across the first 50 rows.
///
/// Use this for vendor exports (invoice lists, item catalogs). For PEE XML
/// (customs-specific), P5.1.7 adds a dedicated target so the mapping can
/// follow the envelope/body structure.
/// </summary>
public class XmlImportParser : IImportFileParser
{
    public ImportSourceFormat Format => ImportSourceFormat.Xml;

    public ParsedImportFile Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        if (doc.Root is null)
            return new ParsedImportFile(Format, Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());

        var rowElements = FindRowElements(doc.Root);
        if (rowElements.Count == 0)
            return new ParsedImportFile(Format, Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());

        var headers = new List<string>();
        var seen = new HashSet<string>();
        foreach (var record in rowElements.Take(50))
        {
            foreach (var attr in record.Attributes())
                if (seen.Add(attr.Name.LocalName)) headers.Add(attr.Name.LocalName);
            foreach (var child in record.Elements())
                if (seen.Add(child.Name.LocalName)) headers.Add(child.Name.LocalName);
        }

        var rows = new List<IReadOnlyList<string?>>();
        foreach (var record in rowElements)
        {
            var row = new List<string?>(headers.Count);
            foreach (var header in headers)
            {
                var attr = record.Attribute(header);
                if (attr != null)
                {
                    row.Add(attr.Value);
                    continue;
                }
                var child = record.Element(record.GetDefaultNamespace() + header)
                            ?? record.Elements().FirstOrDefault(e => e.Name.LocalName == header);
                row.Add(child?.Value);
            }
            rows.Add(row);
        }

        return new ParsedImportFile(Format, headers, rows);
    }

    private static List<XElement> FindRowElements(XElement root)
    {
        // Prefer the most frequent direct-child element name under the root.
        var byName = root.Elements()
            .GroupBy(e => e.Name.LocalName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (byName is not null && byName.Count() >= 1) return byName.ToList();

        // Fallback: if the root has one wrapper child (e.g. <items>), drill in.
        var only = root.Elements().FirstOrDefault();
        if (only != null && only.HasElements)
        {
            var byNameNested = only.Elements()
                .GroupBy(e => e.Name.LocalName)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (byNameNested != null) return byNameNested.ToList();
        }

        return new List<XElement>();
    }
}
