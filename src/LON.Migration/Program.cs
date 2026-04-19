using LON.Migration;
using LON.Migration.Mappers;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: lon-migrate <command> [options]");
    Console.WriteLine("Commands: items | auths | decls | inventory | reconcile | all");
    Console.WriteLine("Options:  --legacy <connStr>  --lon <connStr>  --tenant <code>  --dry-run  --limit <n>");
    return 0;
}

var cmd = args[0];
var argv = args.Skip(1).ToArray();
string? legacy = GetOpt(argv, "--legacy") ?? Environment.GetEnvironmentVariable("MIGRATION_LEGACY");
string? lon = GetOpt(argv, "--lon") ?? Environment.GetEnvironmentVariable("MIGRATION_LON");
string tenantCode = GetOpt(argv, "--tenant") ?? "TEKSPORT";
bool dry = argv.Contains("--dry-run");
int limit = int.TryParse(GetOpt(argv, "--limit"), out var n) ? n : 0;

legacy ??= "Server=localhost;Database=ELON;Trusted_Connection=True;TrustServerCertificate=True";
lon ??= "Server=localhost;Database=LON_Dev;Trusted_Connection=True;TrustServerCertificate=True";

var ctx = new MigrationContext
{
    LegacyConnStr = legacy,
    LonConnStr = lon,
    TenantCode = tenantCode,
    DryRun = dry,
};

Console.WriteLine($"[migrate] cmd={cmd} tenant={tenantCode} dry={dry} limit={limit}");

try
{
    ctx.Hydrate();
    Console.WriteLine($"[migrate] tenant.Id = {ctx.TenantId}");
    Console.WriteLine($"[migrate] defaults: uom={ctx.DefaultUoMId} wh={ctx.DefaultWarehouseId} " +
                      $"loc={ctx.DefaultReceivingLocationId} proc={ctx.InwardProcessingProcedureId}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[migrate] Hydration failed: {ex.Message}");
    return 2;
}

int rc = 0;
switch (cmd)
{
    case "items":     rc = new ItemMapper(ctx).Run(limit); break;
    case "auths":     rc = new AuthorizationMapper(ctx).Run(limit); break;
    case "decls":     rc = new DeclarationMapper(ctx).Run(limit); break;
    case "inventory": rc = new InventoryMapper(ctx).Run(limit); break;
    case "reconcile": rc = new ReconciliationReporter(ctx).Run(); break;
    case "all":
        rc = new ItemMapper(ctx).Run(limit);
        if (rc == 0) rc = new AuthorizationMapper(ctx).Run(limit);
        if (rc == 0) rc = new DeclarationMapper(ctx).Run(limit);
        if (rc == 0) rc = new InventoryMapper(ctx).Run(limit);
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
