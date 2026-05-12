#!/usr/bin/env bash
# Phase 17.PRE.5 — Wipe VPS LONDB to clean slate.
#
# Per docs/migration/TEKSPORT_WIPE_PLAN.md:
#  1. Pre-wipe diagnostic (row counts)
#  2. BACKUP DATABASE LONDB → /var/opt/mssql/backup/LONDB_pre-wipe_<UTC>.bak
#  3. RESTORE VERIFYONLY gate (must pass)
#  4. Wipe (sp_MSforeachtable NOCHECK + DELETE + WITH CHECK CHECK)
#  5. Post-wipe verification (all business tables empty)
#
# Does NOT seed — that's PRE.6.
# Does NOT stop containers — sqlserver stays up; API may be unhappy but not killed.
#
# Run on VPS host (root@173.212.254.216):
#   cd /opt/apps/LON/LON-test && bash scripts/wipe-vps-londb.sh
#
# Exit codes:
#   0 = wipe successful, DB empty
#   1 = backup failed
#   2 = restore verifyonly failed → ABORT wipe
#   3 = wipe failed
#   4 = post-wipe verification failed

set -euo pipefail

SQLSERVER_CONTAINER=lon-sqlserver
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
TS=$(date -u +%Y%m%dT%H%M%SZ)
BACKUP_FILE="/var/opt/mssql/backup/LONDB_pre-wipe_${TS}.bak"

echo "=== Phase 17.PRE.5 — Wipe VPS LONDB ==="
echo "Timestamp (UTC): ${TS}"
echo "Backup target:   ${BACKUP_FILE}"
echo

# Extract SA password from API container env (avoids printing it in shell history)
PASS_B64=$(docker inspect lon-api --format '{{range .Config.Env}}{{println .}}{{end}}' \
  | grep ConnectionStrings__DefaultConnection \
  | sed -E 's/.*Password=//; s/;.*//' \
  | base64 -w0)

# Helper: run sqlcmd inside container, with password injected via base64-decoded env
sql() {
  local query="$1"
  local extra="${2:-}"
  docker exec -e PB64="$PASS_B64" "$SQLSERVER_CONTAINER" bash -c \
    "P=\$(echo \$PB64 | base64 -d); $SQLCMD -S localhost -U sa -P \"\$P\" -N -C -b -I $extra -Q \"$query\""
}

sql_db() {
  local query="$1"
  sql "$query" "-d LONDB"
}

# 1) Pre-wipe diagnostic (top 15 most-populated tables)
echo "--- Pre-wipe top populated tables ---"
sql_db "
SET NOCOUNT ON;
SELECT TOP 15
  t.name AS TableName,
  SUM(p.rows) AS Rws
FROM sys.tables t
INNER JOIN sys.partitions p ON p.object_id = t.object_id
WHERE p.index_id IN (0,1)
GROUP BY t.name
HAVING SUM(p.rows) > 0
ORDER BY SUM(p.rows) DESC;
"
echo

# 2) BACKUP
echo "--- BACKUP DATABASE LONDB ---"
sql "BACKUP DATABASE LONDB TO DISK = N'${BACKUP_FILE}' WITH INIT, COMPRESSION, FORMAT, STATS = 10;" || {
  echo "ERROR: backup failed"
  exit 1
}
echo "Backup completed."
echo

# 3) RESTORE VERIFYONLY gate
echo "--- RESTORE VERIFYONLY gate ---"
sql "RESTORE VERIFYONLY FROM DISK = N'${BACKUP_FILE}';" || {
  echo "ERROR: backup file failed verifyonly — ABORTING wipe"
  exit 2
}
echo "Backup verified."
echo

# 4) Wipe
echo "--- Executing wipe (NOCHECK + DELETE + reseed + CHECK CHECK) ---"
sql_db "
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRAN WipeAll;

-- Disable all FK constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- DELETE everything except migration history
EXEC sp_MSforeachtable 'IF PARSENAME(''?'',1) NOT IN (''__EFMigrationsHistory'') DELETE FROM ?';

-- Reseed identity columns (most tables are Guid PKs; this is a no-op for them)
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'DBCC CHECKIDENT(''' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N''', RESEED, 0);' + CHAR(13)
FROM sys.tables t
JOIN sys.identity_columns c ON c.object_id = t.object_id
WHERE t.name <> '__EFMigrationsHistory';
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

-- Re-enable FKs WITH CHECK
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

COMMIT;
PRINT 'Wipe transaction committed.';
" || {
  echo "ERROR: wipe failed"
  exit 3
}
echo "Wipe completed."
echo

# 5) Post-wipe verification
echo "--- Post-wipe verification ---"
sql_db "
SELECT 'migrations (should be 50)' = COUNT(*) FROM __EFMigrationsHistory;

SELECT
  t.name AS TableName,
  SUM(p.rows) AS Rws
FROM sys.tables t
INNER JOIN sys.partitions p ON p.object_id = t.object_id
WHERE p.index_id IN (0,1)
  AND t.name <> '__EFMigrationsHistory'
GROUP BY t.name
HAVING SUM(p.rows) > 0
ORDER BY t.name;
"

# Final gate: count non-empty tables (excluding migration history)
NON_EMPTY=$(docker exec -e PB64="$PASS_B64" "$SQLSERVER_CONTAINER" bash -c \
  "P=\$(echo \$PB64 | base64 -d); $SQLCMD -S localhost -U sa -P \"\$P\" -N -C -d LONDB -h -1 -W -b -Q \"
SET NOCOUNT ON;
SELECT COUNT(*)
FROM (
  SELECT t.name
  FROM sys.tables t
  INNER JOIN sys.partitions p ON p.object_id = t.object_id
  WHERE p.index_id IN (0,1)
    AND t.name <> '__EFMigrationsHistory'
  GROUP BY t.name
  HAVING SUM(p.rows) > 0
) q;
\"" | tr -d '[:space:]')

if [[ "$NON_EMPTY" == "0" ]]; then
  echo
  echo "✅ Wipe verified — all business tables empty (only __EFMigrationsHistory populated)."
  echo "Backup retained at: ${BACKUP_FILE}"
  echo "Next: PRE.6 (seed) — set LON_BOOTSTRAP_ADMIN_PASSWORD env var then restart lon-api."
  exit 0
else
  echo
  echo "❌ Post-wipe verification FAILED: ${NON_EMPTY} tables still have rows."
  echo "Investigate. Backup available at: ${BACKUP_FILE} for rollback."
  exit 4
fi
