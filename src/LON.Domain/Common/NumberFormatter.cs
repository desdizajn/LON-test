namespace LON.Domain.Common;

/// <summary>
/// Phase 17 §E1 + §E12 — string formatting for per-tenant sequential entity numbers.
///
/// Convention (BLUEPRINT §6.6): `{prefix}-{year:0000}-{seq:D6}`.
/// Examples:
///   ClientOrder(2026, 42) → "CO-2026-000042"
///   CustomsDeclaration(IM, 2026, 17) → "IM-2026-000017"
///
/// The numeric sequence value itself comes from a SQL SEQUENCE
/// (per-tenant; created by migration). This helper is **pure** — no DB
/// access — so it stays trivially unit-testable.
///
/// §E12 will extend this with all numbered entity types (Receipt, Shipment,
/// MaterialIssue, etc.) once their migrators emit SEQUENCEs.
/// </summary>
public static class NumberFormatter
{
    /// <summary>`CO-{year}-{seq:D6}` — Phase 17 §E1 ClientOrder.</summary>
    public static string ClientOrder(int year, long seq) =>
        $"CO-{year:D4}-{seq:D6}";

    /// <summary>`IM-{year}-{seq:D6}` — import customs declaration (§E12).</summary>
    public static string ImDeclaration(int year, long seq) =>
        $"IM-{year:D4}-{seq:D6}";

    /// <summary>`EX-{year}-{seq:D6}` — export customs declaration (§E12).</summary>
    public static string ExDeclaration(int year, long seq) =>
        $"EX-{year:D4}-{seq:D6}";

    /// <summary>`DN-{year}-{seq:D6}` — DeliveryNote (§E7.6, D5).</summary>
    public static string DeliveryNote(int year, long seq) =>
        $"DN-{year:D4}-{seq:D6}";

    /// <summary>`CI-{year}-{seq:D6}` — CommercialInvoice (§E8.5, D4).</summary>
    public static string CommercialInvoice(int year, long seq) =>
        $"CI-{year:D4}-{seq:D6}";
}
