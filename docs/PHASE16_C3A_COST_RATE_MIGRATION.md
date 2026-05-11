# P16.C3.a — one-time migration: `lon.costAccounting.<tenantId>` → CostRate

Old shape (WorkCenter × Shift matrix) was stored at
`localStorage.lon.costAccounting.<tenantId>`. The new entity supports a
single Scope (Machine/Operator/Shift/Operation/WorkCenter) + ScopeId.
The migration snippet flattens each legacy WorkCenter row into a
Scope=WorkCenter CostRate keyed by the WorkCenter id. Shift dimension is
captured in the `notes` field as `shift=<shiftId>` because the new
schema is single-axis. If you need a stricter Shift breakdown after
migration, re-enter the rows with Scope=Shift.

## Steps

Sign in, open DevTools → Console, paste:

```javascript
(async () => {
  const tokenRaw = localStorage.getItem('token');
  if (!tokenRaw) { alert('Sign in first.'); return; }

  const tenantId = (() => {
    try {
      const part = tokenRaw.split('.')[1];
      const p = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
      return p['tenant_id'] || 'default';
    } catch { return 'default'; }
  })();

  const key = `lon.costAccounting.${tenantId}`;
  const raw = localStorage.getItem(key);
  if (!raw) { alert('No local cost-accounting rows to migrate.'); return; }

  const rows = JSON.parse(raw);
  let ok = 0;
  for (const r of rows) {
    // legacy rate is "per minute" — convert to per-hour for the new schema.
    const costPerHour = r.ratePerMinute ? Number(r.ratePerMinute) * 60 : null;
    const payload = {
      scope: 5,                        // WorkCenter
      scopeId: r.workCenterId,
      costPerHour,
      currency: r.currency || 'EUR',
      validFrom: new Date().toISOString().slice(0, 10),
      notes: `${r.notes || ''}${r.shiftId ? `; shift=${r.shiftId}` : ''}`.trim(),
    };
    const resp = await fetch('/api/Finance/cost-rates', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${tokenRaw}` },
      body: JSON.stringify(payload),
    });
    if (resp.ok) ok++;
  }

  if (ok === rows.length) localStorage.removeItem(key);
  console.log(`Migrated ${ok}/${rows.length} cost rows.`);
  alert(`Cost-rate migration done.\nMigrated: ${ok}/${rows.length}\nPage will reload.`);
  location.reload();
})();
```

## Field mapping

| Legacy field          | New CostRate column         |
|-----------------------|-----------------------------|
| `workCenterId`        | `ScopeId` (with Scope=WorkCenter) |
| `ratePerMinute`       | `CostPerHour` (= ratePerMinute × 60) |
| `currency`            | `Currency`                  |
| `notes` + `shiftId`   | `Notes` (shift recorded as `; shift=<id>`) |

## Why convert ratePerMinute → CostPerHour

The legacy page used "per minute" for cost; the new schema is per-hour OR per-unit.
We pick per-hour to preserve durations as the dominant signal. Operators
who prefer per-minute can divide by 60 when reading; downstream margin
calculations multiply by `productionMinutes / 60`.
