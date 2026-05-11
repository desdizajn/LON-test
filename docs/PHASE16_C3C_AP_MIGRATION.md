# P16.C3.c — one-time migration: `lon.supplierInvoices.<tenantId>` → SupplierInvoice

The legacy Supplier Invoices page stored rows in
`localStorage.lon.supplierInvoices.<tenantId>`. The new entity backs
the same page; this snippet uploads each row to the API and clears
the key on success.

## Steps

1. Sign in.
2. DevTools → Console → paste:

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

  const key = `lon.supplierInvoices.${tenantId}`;
  const raw = localStorage.getItem(key);
  if (!raw) { alert('No local supplier invoices to migrate.'); return; }
  const rows = JSON.parse(raw);

  let ok = 0;
  for (const r of rows) {
    if (!r.number || !r.supplierId || !r.amount) continue;
    const create = await fetch('/api/Finance/supplier-invoices', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${tokenRaw}` },
      body: JSON.stringify({
        number: r.number,
        supplierPartnerId: r.supplierId,
        invoiceDate: r.issueDate || new Date().toISOString().slice(0, 10),
        dueDate: r.dueDate || new Date().toISOString().slice(0, 10),
        amount: Number(r.amount),
        currency: r.currency || 'EUR',
        notes: [r.reference, r.notes].filter(Boolean).join(' · ') || null,
      }),
    });
    if (!create.ok) continue;
    const body = await create.json();
    const id = body?.data?.id;
    if (id && (r.status === 'Paid' || r.status === 'Cancelled')) {
      await fetch(`/api/Finance/supplier-invoices/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${tokenRaw}` },
        body: JSON.stringify({
          id,
          number: r.number,
          supplierPartnerId: r.supplierId,
          invoiceDate: r.issueDate,
          dueDate: r.dueDate,
          amount: Number(r.amount),
          currency: r.currency || 'EUR',
          status: r.status === 'Paid' ? 2 : 3,
          paidDate: r.status === 'Paid' ? new Date().toISOString().slice(0, 10) : null,
          notes: [r.reference, r.notes].filter(Boolean).join(' · ') || null,
        }),
      });
    }
    ok++;
  }

  if (ok === rows.length) localStorage.removeItem(key);
  console.log(`Migrated ${ok}/${rows.length} supplier invoice(s).`);
  alert(`Supplier-invoice migration done.\nMigrated: ${ok}/${rows.length}\nPage will reload.`);
  location.reload();
})();
```

## Field mapping + status mapping

| Legacy field    | New column                            |
|-----------------|---------------------------------------|
| `number`        | `Number`                              |
| `supplierId`    | `SupplierPartnerId`                   |
| `issueDate`     | `InvoiceDate`                         |
| `dueDate`       | `DueDate`                             |
| `amount`        | `Amount`                              |
| `currency`      | `Currency`                            |
| `status`        | `Status` (Pending→Open=1; Paid=2; Cancelled=3) |
| `reference`     | concatenated into `Notes`             |
| `notes`         | `Notes`                               |

**Overdue is NOT a stored status.** The backend derives it from
`Status=Open AND DueDate < today` when projecting rows in
`GET /api/Finance/supplier-invoices`. Filtering by `?status=4` returns
only invoices currently in overdue state.
