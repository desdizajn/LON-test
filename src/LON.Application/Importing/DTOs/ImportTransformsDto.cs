namespace LON.Application.Importing.DTOs;

/// <summary>
/// P5.1.4 — per-column transform rules executed at commit time (and for the
/// dry-run preview). Rules are a list of string tokens so the wizard can
/// compose them (e.g. [TRIM, UPPER] runs trim before upper). Supported
/// tokens:
///   <c>TRIM</c>              — whitespace both ends.
///   <c>UPPER</c>             — invariant upper case.
///   <c>LOWER</c>             — invariant lower case.
///   <c>DECIMAL_COMMA_TO_DOT</c> — swap ',' for '.' so European decimals parse.
///   <c>DATE_PARSE:&lt;format&gt;</c> — reparse a date string with the given format and
///                          re-emit as ISO-8601. Invalid dates leave cell untouched.
///   <c>LOOKUP:&lt;Entity&gt;.&lt;FieldName&gt;</c> — resolve cell to the Id of a
///                          matching entity row (DB-backed; executed at commit, not preview).
/// </summary>
public sealed record ImportTransforms(List<ImportColumnTransform> Columns)
{
    public ImportTransforms() : this(new List<ImportColumnTransform>()) { }
}

public sealed record ImportColumnTransform(
    string SourceHeader,
    List<string> Rules);
