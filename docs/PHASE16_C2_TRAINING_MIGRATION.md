# P16.C2 — one-time migration: `lon.training.<tenantId>` → EmployeeCertification

Training records used to persist to a `localStorage` key
`lon.training.<tenantId>`. As of P16.C2 the page talks to the
`EmployeeCertification` backend entity. Existing operator entries
stay in the browser unless this snippet is run.

## Steps

1. Sign in to `https://elon.elbosoft.click` (the snippet uses the JWT
   in `localStorage.token`).
2. Open DevTools → Console.
3. Paste the snippet below, hit Enter.

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

  const key = `lon.training.${tenantId}`;
  const raw = localStorage.getItem(key);
  if (!raw) { alert('No local training records to migrate.'); return; }

  const rows = JSON.parse(raw);
  let ok = 0;
  for (const r of rows) {
    if (!r.employeeId) continue;            // legacy stored employeeId — required
    const payload = {
      employeeId: r.employeeId,
      certificationName: r.topic,
      skillArea: r.skillArea || null,
      issuedDate: r.completionDate,
      expiryDate: r.expiryDate || null,
      issuingAuthority: r.provider || null,
      certificateNumber: r.certificate || null,
      notes: r.notes || null,
    };
    const resp = await fetch('/api/Hr/certifications', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${tokenRaw}` },
      body: JSON.stringify(payload),
    });
    if (resp.ok) ok++;
  }

  if (ok === rows.length) localStorage.removeItem(key);
  console.log(`Migrated ${ok}/${rows.length} training record(s).`);
  alert(`Training migration done.\nMigrated: ${ok}/${rows.length}\nPage will reload.`);
  location.reload();
})();
```

## Verification

After the snippet finishes:
- `/hr/training` → previously local rows appear in the table.
- DevTools → Local Storage → no `lon.training.<tenant>` key remains.
- SQL: `SELECT COUNT(*) FROM EmployeeCertifications WHERE TenantId='<tenant>'`
  matches the number reported.
- `GET /api/Hr/certifications/expiring?withinDays=30` returns rows whose
  expiry falls within 30 days (used for the traffic-light header).

## Field mapping

| Legacy localStorage key            | New BE column         |
|------------------------------------|-----------------------|
| `topic`                            | `CertificationName`   |
| `skillArea`                        | `SkillArea`           |
| `provider`                         | `IssuingAuthority`    |
| `completionDate`                   | `IssuedDate`          |
| `expiryDate`                       | `ExpiryDate`          |
| `certificate`                      | `CertificateNumber`   |
| `notes`                            | `Notes`               |
| `employeeId` (FK)                  | `EmployeeId` (FK)     |
| `employeeName` (cached display)    | resolved via `Include(c => c.Employee)` |
