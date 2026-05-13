using LON.Application.Common.Interfaces;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace LON.Infrastructure.Services;

/// <summary>
/// Phase 17 §E1 — SQL Server-backed implementation of INumberSequenceService.
///
/// Uses `SELECT NEXT VALUE FOR seq_&lt;entity&gt;_&lt;tenantId&gt;`. The sequence
/// must already exist (created by EF migration in §E1 for ClientOrder; §E12
/// adds the rest).
///
/// Sequence name format: `seq_{entityKey}_{tenantId_no_dashes}`. Dashes are
/// stripped from tenantId for the identifier (SQL Server doesn't allow `-`
/// in identifier names without quoting; quoting works but the un-quoted form
/// is friendlier to read in DMVs).
///
/// Thread-safety: `NEXT VALUE FOR` is atomic at the DB level. Concurrent
/// callers under load are SQL Server's responsibility, not ours.
/// </summary>
public class SqlNumberSequenceService : INumberSequenceService
{
    private readonly ApplicationDbContext _context;

    public SqlNumberSequenceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<long> NextAsync(string entityKey, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sequenceName = BuildSequenceName(entityKey, tenantId);

        // Whitelist guard: sequence name must only contain alnum + underscore.
        // (Anchors a SQL-injection block since we interpolate into raw SQL.)
        if (!IsSafeIdentifier(sequenceName))
            throw new InvalidOperationException(
                $"Sequence name '{sequenceName}' is not a safe SQL identifier.");

        var conn = _context.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) await conn.OpenAsync(cancellationToken);

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT NEXT VALUE FOR [{sequenceName}];";
            cmd.CommandType = System.Data.CommandType.Text;
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        finally
        {
            if (wasClosed) await conn.CloseAsync();
        }
    }

    /// <summary>Build a SQL identifier-safe sequence name.</summary>
    public static string BuildSequenceName(string entityKey, Guid tenantId)
    {
        return $"seq_{entityKey}_{tenantId:N}";
    }

    private static bool IsSafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }
}
