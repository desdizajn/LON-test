# P16.C3.b — one-time migration: `lon.payrollRates.<tenantId>` → operator memory

The legacy Payroll page stored per-employee hourly rates in
`localStorage.lon.payrollRates.<tenantId>` and computed regular / overtime
pay client-side. The new `PayrollPeriod` + `PayrollLine` schema does NOT
store rates; instead the operator enters the final `NetAmount` per line
once per period.

Because there's no direct mapping for rates on the new schema, there's
no automated migration. Run this snippet to compute the suggested
NetAmount per employee for the current month using the legacy rate ×
attendance hours, then patch each PayrollLine via the new API.

## Steps

1. Sign in.
2. Visit `/finance/payroll`. Pick the month and click "Create period for
   this month". This seeds lines with hours from Attendance + Absence.
3. Open DevTools → Console. Paste:

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

  const ratesRaw = localStorage.getItem(`lon.payrollRates.${tenantId}`);
  if (!ratesRaw) { alert('No legacy rates to apply.'); return; }
  const rates = JSON.parse(ratesRaw); // [{employeeId, ratePerHour, currency}]
  const rateByEmp = new Map(rates.map((r) => [r.employeeId, r.ratePerHour]));

  const url = `/api/Finance/payroll-periods`;
  const list = await fetch(url, { headers: { Authorization: `Bearer ${tokenRaw}` } })
    .then((r) => r.json());
  if (!list.isSuccess || !list.data.length) { alert('No payroll periods. Create one first.'); return; }

  // Apply rates to the most recent Draft period only.
  const target = list.data.find((p) => p.status === 1);
  if (!target) { alert('No Draft period to patch.'); return; }

  const overtimeX = 1.5;
  let ok = 0;
  for (const line of target.lines) {
    const rate = rateByEmp.get(line.employeeId);
    if (!rate) continue;
    const net = rate * line.regularHours + rate * line.overtimeHours * overtimeX
              + line.bonusAmount - line.deductionAmount;
    const resp = await fetch(`/api/Finance/payroll-periods/lines/${line.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${tokenRaw}` },
      body: JSON.stringify({
        id: line.id,
        regularHours: line.regularHours,
        overtimeHours: line.overtimeHours,
        absenceHours: line.absenceHours,
        bonusAmount: line.bonusAmount,
        deductionAmount: line.deductionAmount,
        netAmount: Math.round(net * 100) / 100,
        currency: line.currency,
      }),
    });
    if (resp.ok) ok++;
  }

  // Clear legacy key only after a successful sweep.
  if (ok > 0) localStorage.removeItem(`lon.payrollRates.${tenantId}`);
  alert(`Patched ${ok} payroll lines. Reload the page to see updated NetAmounts.`);
  location.reload();
})();
```

## Notes

- The snippet uses 1.5× overtime multiplier (legacy default). Adjust the
  `overtimeX` constant if your tenant used another factor.
- Only the most recent **Draft** period is patched. Finalized / Exported
  periods cannot be edited; create a new one if you need to restate.
- After patching the legacy `lon.payrollRates` key is cleared. Future
  periods are seeded from Attendance + Absence and the operator enters
  NetAmount manually (or pastes a re-run of this snippet pointing at a
  fresh Draft period).

## Field semantics on the new schema

| Field             | Source                                |
|-------------------|---------------------------------------|
| `RegularHours`    | Σ Attendance hours capped at standardMonthlyHours |
| `OvertimeHours`   | Σ Attendance hours above standardMonthlyHours     |
| `AbsenceHours`    | Σ approved Absence days × standardHoursPerDay     |
| `BonusAmount`     | Operator-entered                       |
| `DeductionAmount` | Operator-entered                       |
| `NetAmount`       | Operator-entered (final payroll amount)|
| `Currency`        | Defaults EUR; per-line override allowed|
