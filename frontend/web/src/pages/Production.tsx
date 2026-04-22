import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { productionApi } from '../services/api';
import ProductionOrderForm from '../components/Production/ProductionOrderForm';
import MaterialIssueForm from '../components/Production/MaterialIssueForm';
import ProductionReceiptForm from '../components/Production/ProductionReceiptForm';

const Production: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showOrderForm, setShowOrderForm] = useState(false);
  const [showIssueForm, setShowIssueForm] = useState(false);
  const [showReceiptForm, setShowReceiptForm] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | undefined>();
  const [busyOrderId, setBusyOrderId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');

  useEffect(() => {
    loadOrders();
  }, []);

  const loadOrders = async () => {
    try {
      setLoading(true);
      const response = await productionApi.getOrders();
      setOrders(response.data);
    } catch (err) {
      console.error('Failed to load production orders', err);
    } finally {
      setLoading(false);
    }
  };

  const handleFormSuccess = () => {
    setShowOrderForm(false);
    setShowIssueForm(false);
    setShowReceiptForm(false);
    setSelectedOrderId(undefined);
    loadOrders();
  };
  const handleFormCancel = () => {
    setShowOrderForm(false);
    setShowIssueForm(false);
    setShowReceiptForm(false);
    setSelectedOrderId(undefined);
  };

  const handleIssue = (id: string) => { setSelectedOrderId(id); setShowIssueForm(true); };
  const handleReceipt = (id: string) => { setSelectedOrderId(id); setShowReceiptForm(true); };

  const handleRelease = async (id: string) => {
    if (!window.confirm(t('production.releaseConfirm'))) return;
    setBusyOrderId(id);
    try { await productionApi.releaseOrder(id); await loadOrders(); }
    catch (e: any) { alert(e?.response?.data?.errorMessage || t('production.releaseFailed')); }
    finally { setBusyOrderId(null); }
  };
  const handleBulkIssue = async (id: string) => {
    if (!window.confirm(t('production.bulkIssueConfirm'))) return;
    setBusyOrderId(id);
    try { await productionApi.issueAllMaterials(id, new Date().toISOString()); await loadOrders(); }
    catch (e: any) { alert(e?.response?.data?.errorMessage || t('production.bulkIssueFailed')); }
    finally { setBusyOrderId(null); }
  };

  const getStatusBadge = (status: number) => {
    const m: any = {
      1: { label: 'Draft', class: 'info' }, 2: { label: 'Released', class: 'warning' },
      3: { label: 'In Progress', class: 'warning' }, 4: { label: 'Completed', class: 'success' },
      5: { label: 'Closed', class: 'info' }, 6: { label: 'Cancelled', class: 'danger' },
      0: { label: 'Draft', class: 'info' },
    };
    const s = m[status] || { label: 'Unknown', class: 'info' };
    return <span className={`badge badge-${s.class}`}>{s.label}</span>;
  };

  // Group: one bucket per MainOrderNumber (or full OrderNumber when no parent chain).
  // A bucket with >1 row becomes an expandable parent + child list.
  const grouped = useMemo(() => {
    const q = search.trim().toLowerCase();
    const matched = orders.filter((o) => {
      if (statusFilter && String(o.status) !== statusFilter) return false;
      if (q) {
        const hay = `${o.orderNumber ?? ''} ${o.mainOrderNumber ?? ''} ${o.subOrderNumber ?? ''} ${o.item?.code ?? ''} ${o.item?.name ?? ''} ${o.customerOrderNumber ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
    const by: Record<string, { main?: any; children: any[] }> = {};
    for (const o of matched) {
      const key = o.mainOrderNumber || o.orderNumber;
      if (!by[key]) by[key] = { children: [] };
      if (!o.subOrderNumber && o.parentOrderId == null) by[key].main = o;
      else by[key].children.push(o);
    }
    return Object.entries(by)
      .map(([key, v]) => ({ key, main: v.main, children: v.children.sort((a: any, b: any) => (a.subOrderNumber || '').localeCompare(b.subOrderNumber || '')) }))
      .sort((a, b) => a.key.localeCompare(b.key));
  }, [orders, search, statusFilter]);

  const toggle = (k: string) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(k)) next.delete(k); else next.add(k);
      return next;
    });
  };

  const renderActions = (o: any) => (
    <div style={{ display: 'flex', gap: '5px', flexWrap: 'wrap' }}>
      {(o.status === 0 || o.status === 1) && (
        <button className="btn btn-sm" onClick={() => handleRelease(o.id)} disabled={busyOrderId === o.id} title={t('production.release')}>
          {t('production.release')}
        </button>
      )}
      {(o.status === 2 || o.status === 3) && (
        <>
          <button className="btn btn-sm btn-primary" onClick={() => handleIssue(o.id)}>Issue</button>
          <button className="btn btn-sm" onClick={() => handleBulkIssue(o.id)} disabled={busyOrderId === o.id} title={t('production.bulkIssue')}>
            {t('production.bulkIssue')}
          </button>
          <button className="btn btn-sm btn-success" onClick={() => handleReceipt(o.id)}>Receive</button>
        </>
      )}
    </div>
  );

  const variantBadges = (item: any) => (
    <>
      {item?.colorCode && (
        <span style={{ background: '#fef3c7', color: '#92400e', padding: '1px 6px', borderRadius: 3, fontSize: 11, marginLeft: 4 }}>
          🎨 {item.colorCode}
        </span>
      )}
      {item?.sizeCode && (
        <span style={{ background: '#dbeafe', color: '#1e40af', padding: '1px 6px', borderRadius: 3, fontSize: 11, marginLeft: 4 }}>
          📏 {item.sizeCode}
        </span>
      )}
    </>
  );

  if (showOrderForm) return <ProductionOrderForm onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  if (showIssueForm) return <MaterialIssueForm productionOrderId={selectedOrderId} onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  if (showReceiptForm) return <ProductionReceiptForm productionOrderId={selectedOrderId} onSuccess={handleFormSuccess} onCancel={handleFormCancel} />;
  if (loading) return <div className="loading">Loading production orders...</div>;

  return (
    <div>
      <div className="header">
        <h2>Production Orders (LON)</h2>
        <button className="btn btn-success" onClick={() => setShowOrderForm(true)}>+ New Production Order</button>
      </div>

      <div style={{
        display: 'flex',
        gap: 8,
        margin: '12px 0',
        padding: 10,
        background: 'var(--ink-50, #f8fafc)',
        border: '1px solid var(--border, #e5e7eb)',
        borderRadius: 6,
        alignItems: 'center',
        flexWrap: 'wrap',
      }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('production.searchPlaceholder', 'Пребарај по број / артикл / клиентски налог...') as string}
          style={{ padding: 6, minWidth: 280 }}
        />
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} style={{ padding: 6 }}>
          <option value="">{t('production.statusAll', 'Сите статуси')}</option>
          <option value="0">Draft</option>
          <option value="1">Draft</option>
          <option value="2">Released</option>
          <option value="3">In Progress</option>
          <option value="4">Completed</option>
          <option value="5">Closed</option>
          <option value="6">Cancelled</option>
        </select>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th style={{ width: 30 }}></th>
              <th>Order Number</th>
              <th>Item</th>
              <th>Order Qty</th>
              <th>Produced</th>
              <th>Scrap</th>
              <th>Status</th>
              <th>Start</th>
              <th>End</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {grouped.map(g => {
              const hasChildren = g.children.length > 0;
              const isOpen = expanded.has(g.key);
              const headRow = g.main || g.children[0];
              const isStandalone = !g.main && g.children.length === 1;
              return (
                <React.Fragment key={g.key}>
                  <tr style={{ background: hasChildren && g.main ? '#f3f4f6' : undefined, fontWeight: hasChildren ? 600 : undefined }}>
                    <td style={{ textAlign: 'center' }}>
                      {hasChildren && !isStandalone && (
                        <button onClick={() => toggle(g.key)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: 14 }}>
                          {isOpen ? '▼' : '▶'}
                        </button>
                      )}
                    </td>
                    <td>
                      <strong>{g.main?.orderNumber || g.key}</strong>
                      {hasChildren && g.main && (
                        <span style={{ color: '#6b7280', fontSize: 11, marginLeft: 6 }}>
                          ({g.children.length} {t('production.variants', { defaultValue: 'variants' })})
                        </span>
                      )}
                    </td>
                    <td>
                      {headRow?.item?.name || headRow?.item?.code}
                      {!g.main && variantBadges(headRow?.item)}
                    </td>
                    <td>{headRow?.orderQuantity?.toFixed?.(2) ?? '-'}</td>
                    <td>{headRow?.producedQuantity?.toFixed?.(2) ?? '-'}</td>
                    <td>{headRow?.scrapQuantity?.toFixed?.(2) ?? '-'}</td>
                    <td>{getStatusBadge(headRow?.status)}</td>
                    <td>{headRow?.plannedStartDate ? new Date(headRow.plannedStartDate).toLocaleDateString() : '-'}</td>
                    <td>{headRow?.plannedEndDate ? new Date(headRow.plannedEndDate).toLocaleDateString() : '-'}</td>
                    <td>{g.main ? renderActions(g.main) : renderActions(headRow)}</td>
                  </tr>

                  {hasChildren && isOpen && g.children.map((c: any) => (
                    <tr key={c.id} style={{ background: '#ffffff' }}>
                      <td></td>
                      <td style={{ paddingLeft: 26, color: '#4b5563' }}>
                        ↳ <span style={{ fontFamily: 'monospace' }}>{c.subOrderNumber || c.orderNumber}</span>
                      </td>
                      <td>
                        {c.item?.code}
                        {variantBadges(c.item)}
                      </td>
                      <td>{c.orderQuantity.toFixed(2)}</td>
                      <td>{c.producedQuantity.toFixed(2)}</td>
                      <td>{c.scrapQuantity.toFixed(2)}</td>
                      <td>{getStatusBadge(c.status)}</td>
                      <td>{new Date(c.plannedStartDate).toLocaleDateString()}</td>
                      <td>{new Date(c.plannedEndDate).toLocaleDateString()}</td>
                      <td>{renderActions(c)}</td>
                    </tr>
                  ))}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Production;
