using System.Globalization;
using LON.Application.Importing.DTOs;

namespace LON.Application.Common.Importing;

/// <summary>
/// Pure in-process transform engine. Every rule supported by <see cref="ImportTransforms"/>
/// except <c>LOOKUP:*</c> is a string→string computation executed here.
/// LOOKUP is flagged and deferred to the commit handler (P5.1.6) where
/// the DbContext is available.
/// </summary>
public static class ImportTransformRunner
{
    /// <summary>Returns true if the rule is executable without DB access.</summary>
    public static bool IsInMemoryRule(string rule) =>
        !rule.StartsWith("LOOKUP:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies every in-memory rule on the list, left-to-right. LOOKUP rules
    /// are skipped here (commit handler runs them with DB access).
    /// Invalid rules (e.g. a malformed DATE_PARSE format) leave the cell
    /// unchanged — the validation suite (P5.1.5) reports them as warnings.
    /// </summary>
    public static string? Apply(string? value, IEnumerable<string> rules)
    {
        var current = value;
        foreach (var raw in rules)
        {
            var rule = raw?.Trim();
            if (string.IsNullOrWhiteSpace(rule)) continue;
            if (!IsInMemoryRule(rule)) continue;
            current = ApplyOne(current, rule);
        }
        return current;
    }

    private static string? ApplyOne(string? value, string rule)
    {
        if (value is null) return null;
        if (rule.Equals("TRIM", StringComparison.OrdinalIgnoreCase)) return value.Trim();
        if (rule.Equals("UPPER", StringComparison.OrdinalIgnoreCase)) return value.ToUpperInvariant();
        if (rule.Equals("LOWER", StringComparison.OrdinalIgnoreCase)) return value.ToLowerInvariant();
        if (rule.Equals("DECIMAL_COMMA_TO_DOT", StringComparison.OrdinalIgnoreCase))
            return value.Replace(',', '.');

        if (rule.StartsWith("DATE_PARSE:", StringComparison.OrdinalIgnoreCase))
        {
            var fmt = rule.Substring("DATE_PARSE:".Length).Trim();
            if (string.IsNullOrEmpty(fmt)) return value;
            if (DateTime.TryParseExact(value.Trim(), fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                return parsed.ToString("o", CultureInfo.InvariantCulture);
            return value; // leave untouched; commit-time validation will flag it
        }

        return value;
    }
}
