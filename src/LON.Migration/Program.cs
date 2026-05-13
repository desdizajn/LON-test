using LON.Migration;
using LON.Migration.Mappers;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: lon-migrate <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  items       — tblArtikli → Item");
    Console.WriteLine("  partners    — Partner catalog builder (numeric FKs → Partner rows)");
    Console.WriteLine("  odobrenija  — Odobrenija → LONAuthorization");
    Console.WriteLine("  orders      — Zaklucoci → ClientOrder");
    Console.WriteLine("  decls       — FakturiU5Z + FakturiU5 → CustomsDeclaration + Line");
    Console.WriteLine("  fgs         — GotoviProizvodi → ClientOrderFinishedGood");
    Console.WriteLine("  boms        — Normativi → BOM + BOMLine");
    Console.WriteLine("  inventory   — LagerMaterijali → InventoryMovement (+ recompute InventoryBalance)");
    Console.WriteLine("  issues      — Aggregate Proces=7 → ProductionOrder + MaterialIssue + DeliveryNote");
    Console.WriteLine("  wastes      — Aggregate Proces=9 → CustomsDeclaration(type=Waste) + lines");
    Console.WriteLine("  reconcile   — Run R1..R6 reconciliation queries with PASS/FAIL output");
    Console.WriteLine("  all         — All of the above in correct order");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --legacy <connStr>      Legacy ELON DB connection");
    Console.WriteLine("  --lon <connStr>         Target LON DB connection");
    Console.WriteLine("  --tenant <code>         Tenant code (default: TEKSPORT)");
    Console.WriteLine("  --zaklucok <broj>       Filter all mappers to a single Zaklucok (happy-path drill)");
    Console.WriteLine("  --dry-run               Read-only; no INSERTs/UPDATEs");
    Console.WriteLine("  --limit <n>             Cap rows per mapper (TOP n)");
    return 0;
}

var cmd = args[0];
var argv = args.Skip(1).ToArray();
string? legacy = GetOpt(argv, "--legacy") ?? Environment.GetEnvironmentVariable("MIGRATION_LEGACY");
string? lon = GetOpt(argv, "--lon") ?? Environment.GetEnvironmentVariable("MIGRATION_LON");
string tenantCode = GetOpt(argv, "--tenant") ?? "TEKSPORT";
string? zaklucok = GetOpt(argv, "--zaklucok");
bool dry = argv.Contains("--dry-run");
int limit = int.TryParse(GetOpt(argv, "--limit"), out var n) ? n : 0;

legacy ??= "Server=localhost;Database=ELON;Trusted_Connection=True;TrustServerCertificate=True";
lon ??= "Server=localhost;Database=LONDB;Trusted_Connection=True;TrustServerCertificate=True";

var ctx = new MigrationContext
{
    LegacyConnStr = legacy,
    LonConnStr = lon,
    TenantCode = tenantCode,
    ZaklucokFilter = zaklucok,
    DryRun = dry,
};

Console.WriteLine($"[migrate] cmd={cmd} tenant={tenantCode} zaklucok={zaklucok ?? "(all)"} dry={dry} limit={limit}");

try
{
    ctx.Hydrate();
    Console.WriteLine($"[migrate] tenant.Id = {ctx.TenantId}");
    Console.WriteLine($"[migrate] procedures: " + string.Join(", ", ctx.ProcedureByCode.Keys));
    Console.WriteLine($"[migrate] defaults: uom={ctx.DefaultUoMId} wh={ctx.DefaultWarehouseId} " +
                      $"loc.recv={ctx.DefaultReceivingLocationId} loc.prod={ctx.DefaultProductionLocationId}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[migrate] Hydration failed: {ex.Message}");
    return 2;
}

int rc = 0;
switch (cmd)
{
    case "items":       rc = new ItemMapper(ctx).Run(limit); break;
    case "partners":    rc = new PartnerCatalogBuilder(ctx).Run(); break;
    case "odobrenija":  rc = new OdobrenijaMapper(ctx).Run(); break;
    case "orders":      rc = new ClientOrderMapper(ctx).Run(); break;
    case "decls":       rc = new DeclarationMapper(ctx).Run(limit); break;
    case "fgs":         rc = new FinishedGoodMapper(ctx).Run(); break;
    case "boms":        rc = new BOMMapper(ctx).Run(); break;
    case "inventory":   rc = new InventoryMapper(ctx).Run(limit); break;
    case "issues":      rc = new MaterialIssueMapper(ctx).Run(); break;
    case "wastes":      rc = new WasteDeclarationMapper(ctx).Run(); break;
    case "reconcile":   rc = new ReconciliationReporter(ctx).Run(); break;
    case "all":
        rc = new ItemMapper(ctx).Run(limit);
        if (rc == 0) rc = new PartnerCatalogBuilder(ctx).Run();
        if (rc == 0) rc = new OdobrenijaMapper(ctx).Run();
        if (rc == 0) rc = new ClientOrderMapper(ctx).Run();
        if (rc == 0) rc = new DeclarationMapper(ctx).Run(limit);
        if (rc == 0) rc = new FinishedGoodMapper(ctx).Run();
        if (rc == 0) rc = new BOMMapper(ctx).Run();
        if (rc == 0) rc = new InventoryMapper(ctx).Run(limit);
        if (rc == 0) rc = new MaterialIssueMapper(ctx).Run();
        if (rc == 0) rc = new WasteDeclarationMapper(ctx).Run();
        if (rc == 0) rc = new ReconciliationReporter(ctx).Run();
        break;
    default:
        Console.Error.WriteLine($"[migrate] Unknown command: {cmd}");
        rc = 1;
        break;
}

Console.WriteLine($"[migrate] done rc={rc}");
return rc;

static string? GetOpt(string[] a, string name)
{
    for (int i = 0; i < a.Length - 1; i++)
        if (a[i] == name) return a[i + 1];
    return null;
}
