import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { productionApi } from '../services/api';
import ProductionOrderForm from '../components/Production/ProductionOrderForm';
import MaterialIssueForm from '../components/Production/MaterialIssueForm';
import ProductionReceiptForm from '../components/Production/ProductionReceiptForm';
import DataTable, { Column } from '../components/common/DataTable';
import ProductionVariantsSubTable from '../components/Production/ProductionVariantsSubTable';

const Production: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showOrderForm, setShowOrderForm] = useState(false);
  const [showIssueForm, setShowIssueForm] = useState(false);
  const [showReceiptForm, setShowReceiptForm] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | undefined>();
  const [busyOrderId, setBusyOrderId] = useState<string | null>(null);
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

  const tableRows = useMemo<OrderRow[]>(() => grouped.map((g) => {
    const hasChildren = g.children.length > 0;
    const headRow = g.main || g.children[0];
    const isStandalone = !g.main && g.children.length === 1;
    const hasExpandableChildren = hasChildren && !isStandalone;
    return {
      id: g.key,
      orderNumber: g.main?.orderNumber || g.key,
      itemLabel: headRow?.item?.name || headRow?.item?.code || '',
      itemNode: (
        <>
          {headRow?.item?.name || headRow?.item?.code}
          {!g.main && variantBadges(headRow?.item)}
          {hasChildren && g.main && (
            <span style={{ color: '#6b7280', fontSize: 11, marginLeft: 6 }}>
              ({g.children.length} {t('production.variants', { defaultValue: 'variants' })})
            </span>
          )}
        </>
      ),
      orderQty: headRow?.orderQuantity ?? null,
      produced: headRow?.producedQuantity ?? null,
      scrap: headRow?.scrapQuantity ?? null,
      status: headRow?.status ?? null,
      start: headRow?.plannedStartDate ? new Date(headRow.plannedStartDate).toLocaleDateString() : '-',
      end: headRow?.plannedEndDate ? new Date(headRow.plannedEndDate).toLocaleDateString() : '-',
      actions: renderActions(g.main || headRow),
      hasExpandableChildren,
      children: g.children,
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [grouped, busyOrderId, t]);

  const columns: Column<OrderRow>[] = [
    {
      id: 'orderNumber',
      label: t('production.columns.orderNumber', { defaultValue: 'Order Number' }) as string,
      format: (v: string, row) => (
        <strong>{v}</strong>
      ),
    },
    {
      id: 'itemNode',
      label: t('production.columns.item', { defaultValue: 'Item' }) as string,
      format: (_v, row) => row.itemNode,
    },
    {
      id: 'orderQty',
      label: t('production.columns.orderQty', { defaultValue: 'Order Qty' }) as string,
      align: 'right',
      format: (v: number | null) => (v == null ? '-' : v.toFixed(2)),
    },
    {
      id: 'produced',
      label: t('production.columns.produced', { defaultValue: 'Produced' }) as string,
      align: 'right',
      format: (v: number | null) => (v == null ? '-' : v.toFixed(2)),
    },
    {
      id: 'scrap',
      label: t('production.columns.scrap', { defaultValue: 'Scrap' }) as string,
      align: 'right',
      format: (v: number | null) => (v == null ? '-' : v.toFixed(2)),
    },
    {
      id: 'status',
      label: t('production.columns.status', { defaultValue: 'Status' }) as string,
      format: (v: number | null) => (v == null ? '-' : getStatusBadge(v)),
    },
    {
      id: 'start',
      label: t('production.columns.start', { defaultValue: 'Start' }) as string,
    },
    {
      id: 'end',
      label: t('production.columns.end', { defaultValue: 'End' }) as string,
    },
    {
      id: 'actions',
      label: t('common.actions', { defaultValue: 'Actions' }) as string,
      format: (_v, row) => row.actions,
    },
  ];

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

      <DataTable<OrderRow>
        columns={columns}
        data={tableRows}
        searchable={false}
        emptyMessage={t('production.empty', { defaultValue: 'No production orders' }) as string}
        renderExpanded={(row) =>
          row.hasExpandableChildren ? (
            <ProductionVariantsSubTable
              children={row.children}
              variantBadges={variantBadges}
              getStatusBadge={getStatusBadge}
              renderActions={renderActions}
            />
          ) : null
        }
        rowClassName={(row) => (row.hasExpandableChildren ? 'group-parent-row' : undefined)}
      />
    </div>
  );
};

type OrderRow = {
  id: string;
  orderNumber: string;
  itemLabel: string;
  itemNode: React.ReactNode;
  orderQty: number | null;
  produced: number | null;
  scrap: number | null;
  status: number | null;
  start: string;
  end: string;
  actions: React.ReactNode;
  hasExpandableChildren: boolean;
  children: any[];
};

export default Production;
