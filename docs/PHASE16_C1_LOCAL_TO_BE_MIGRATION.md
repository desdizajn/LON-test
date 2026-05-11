# P16.C1 — one-time migration: localStorage → RiskRegisterItem backend

Risks and Escalations used to persist to `localStorage` keys
`lon.risks.<tenantId>` and `lon.escalations.<tenantId>`. As of P16.C1
the pages talk to the `RiskRegisterItem` backend entity. Operators who
already entered data into the browser will see their list go empty the
first time they open the new build.

This document is a paste-once browser console snippet that uploads the
local rows into the new endpoints, then clears the keys.

## Steps

1. Sign in normally to `https://elon.elbosoft.click` (or your tenant
   instance) — the API uses the same JWT that lives in `localStorage.token`.
2. Open DevTools → Console.
3. Paste the snippet below, hit Enter.
4. The snippet logs progress, then refreshes the page on success.

If anything fails, **do not** clear the localStorage keys manually —
re-run the snippet, since it is idempotent up to the point of clearing.

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

  const SEV_TO_NUM = { Low: 1, Medium: 2, High: 3, Critical: 4 };
  const RISK_STATUS_TO_NUM = { Open: 1, Mitigating: 3, Closed: 6 };
  const ESC_STATUS_TO_NUM  = { Open: 1, InReview: 2, Resolved: 4, Deferred: 5 };

  async function post(payload) {
    const r = await fetch('/api/Management/risks', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${tokenRaw}` },
      body: JSON.stringify(payload),
    });
    return r.ok;
  }

  // ─── Risks (Kind=1) ───
  const risksKey = `lon.risks.${tenantId}`;
  const risksRaw = localStorage.getItem(risksKey);
  let risksCount = 0;
  if (risksRaw) {
    const rows = JSON.parse(risksRaw);
    for (const r of rows) {
      const ok = await post({
        kind: 1,
        title: r.title,
        category: r.category || null,
        severity: SEV_TO_NUM[r.severity] || 2,
        status: RISK_STATUS_TO_NUM[r.status] || 1,
        owner: r.owner || null,
        mitigation: r.mitigation || null,
        reviewDate: r.reviewDate || null,
      });
      if (ok) risksCount++;
    }
    if (risksCount === rows.length) localStorage.removeItem(risksKey);
  }

  // ─── Escalations (Kind=2) ───
  const escKey = `lon.escalations.${tenantId}`;
  const escRaw = localStorage.getItem(escKey);
  let escCount = 0;
  if (escRaw) {
    const rows = JSON.parse(escRaw);
    for (const r of rows) {
      const ok = await post({
        kind: 2,
        title: r.title,
        category: r.party || null,           // legacy `party` → unified `category`
        severity: SEV_TO_NUM[r.severity] || 2,
        status: ESC_STATUS_TO_NUM[r.status] || 1,
        owner: r.owner || null,
        mitigation: r.description || null,   // legacy `description` → `mitigation`
        resolution: r.resolution || null,
        dueDate: r.dueDate || null,
      });
      if (ok) escCount++;
    }
    if (escCount === rows.length) localStorage.removeItem(escKey);
  }

  console.log(`Migrated ${risksCount} risk(s) + ${escCount} escalation(s).`);
  alert(`Migration done.\nRisks:        ${risksCount}\nEscalations:  ${escCount}\nPage will reload.`);
  location.reload();
})();
```

## Verification

After the snippet finishes:
- `/management/risks` → previously local rows should appear in the table.
- `/management/escalations` → same.
- DevTools → Application → Local Storage → no `lon.risks.*` / `lon.escalations.*`
  keys remain for the migrated tenant.
- SSMS / VPS DB: `SELECT COUNT(*) FROM RiskRegisterItems WHERE TenantId = '<tenant>'`
  matches the number reported by the snippet.

## Field mapping reference

| Legacy localStorage shape | New BE column                 |
|---------------------------|-------------------------------|
| `severity` ("High", ...)  | `Severity` (enum int 1–4)     |
| `status` ("Open", ...)    | `Status` (enum int 1–6)       |
| `category` (Risk)         | `Category`                    |
| `party` (Escalation)      | `Category` (unified)          |
| `description` (Escalation)| `Mitigation`                  |
| `mitigation` (Risk)       | `Mitigation`                  |
| `resolution`              | `Resolution`                  |
| `reviewDate`              | `ReviewDate`                  |
| `dueDate`                 | `DueDate`                     |

The unified schema is documented in
[`src/LON.Domain/Entities/Management/RiskRegisterItem.cs`](../src/LON.Domain/Entities/Management/RiskRegisterItem.cs).
