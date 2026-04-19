using System.Security.Cryptography;
using System.Text;

namespace LON.Migration;

internal static class Helpers
{
    /// <summary>
    /// MD5-derived deterministic Guid from a composite key.
    /// Used so re-running the migrator maps the same legacy row to the same LON row.
    /// </summary>
    public static Guid DeterministicGuid(string kind, string key)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{kind}|{key}"));
        return new Guid(bytes);
    }

    public static string? AsString(object? o) => o is DBNull or null ? null : o.ToString()?.Trim();

    public static string AsStringOrEmpty(object? o) => AsString(o) ?? string.Empty;

    public static decimal AsDecimal(object? o)
    {
        if (o is DBNull or null) return 0m;
        return Convert.ToDecimal(o);
    }

    public static int AsInt(object? o)
    {
        if (o is DBNull or null) return 0;
        return Convert.ToInt32(o);
    }

    public static DateTime? AsDate(object? o)
    {
        if (o is DBNull or null) return null;
        return Convert.ToDateTime(o);
    }

    public static DateTime AsDateOrNow(object? o) => AsDate(o) ?? DateTime.UtcNow;

    public static bool AsBool(object? o)
    {
        if (o is DBNull or null) return false;
        return Convert.ToBoolean(o);
    }
}
