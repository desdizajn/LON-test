# LON.Migration — Legacy ELON → LON data migration

One-shot console tool. Reads from the legacy ELON SQL Server database (read-only), writes
to the LON SQL Server database. Idempotent: re-running the same command upserts.

## Usage

```bash
# dry run (print counts, no writes)
dotnet run --project src/LON.Migration -- items --dry-run

# real run against local dev LON DB
dotnet run --project src/LON.Migration -- all \
  --legacy "Server=localhost;Database=ELON;Trusted_Connection=True;TrustServerCertificate=True" \
  --lon    "Server=localhost;Database=LON_Dev;Trusted_Connection=True;TrustServerCertificate=True" \
  --tenant TEKSPORT
```

## Subcommands

| cmd | source tables | target |
|---|---|---|
| `items` | `tblArtikli` | `Items` |
| `auths` | `Odobrenija`, `Zaklucoci` | `LONAuthorizations` |
| `decls` | `FakturiU5Z` + `FakturiU5` | `CustomsDeclarations` + Lines |
| `inventory` | `LagerMaterijali` (aggregated) | `InventoryBalances` |
| `reconcile` | — | writes `migration_reconciliation.html` |
| `all` | all of the above | |

## Determinism / idempotency

New LON GUIDs are derived from `(entityType, legacyId)` via MD5 hash of the composite key.
Re-running the tool maps the same legacy row to the same LON row every time — an UPSERT.
No schema changes to existing entities are required; a one-row import_map table is NOT used.

## Safety

- Legacy DB connection is opened read-only (no DDL, no DML).
- The tool will refuse to run if `--tenant` resolves to a missing tenant in LON.
- `--dry-run` prints planned counts; skip this and you get real writes.
