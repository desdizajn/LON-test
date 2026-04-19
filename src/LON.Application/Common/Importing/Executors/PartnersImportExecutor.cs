using LON.Application.Common.Interfaces;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

public class PartnersImportExecutor : IImportTargetExecutor
{
    public string TargetName => "Partners";

    public async Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = row.GetOrDefault<string>("code");
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (!codesSeen.Add(code))
                return (false, 0, $"Row {row.RowIndex}: duplicate code '{code}' within the same file.");
        }

        int created = 0;
        foreach (var row in rows)
        {
            var code = row.GetOrDefault<string>("code")!;
            var exists = await context.Partners.AnyAsync(p => p.Code == code, cancellationToken);
            if (exists)
                return (false, created, $"Row {row.RowIndex}: Partner code '{code}' is already taken.");

            var typeName = row.GetOrDefault<string>("type") ?? "Supplier";
            if (!Enum.TryParse<PartnerType>(typeName, ignoreCase: true, out var type))
                return (false, created, $"Row {row.RowIndex}: invalid partner type '{typeName}'.");

            var partner = new Partner
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = row.GetOrDefault<string>("name") ?? code,
                Type = type,
                TaxNumber = row.GetOrDefault<string>("taxId"),
                Address = row.GetOrDefault<string>("address"),
                Country = row.GetOrDefault<string>("country"),
                Email = row.GetOrDefault<string>("email"),
                Phone = row.GetOrDefault<string>("phone"),
                IsActive = true
            };
            await context.Partners.AddAsync(partner, cancellationToken);
            created++;
        }
        return (true, created, null);
    }
}
