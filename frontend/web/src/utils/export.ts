import i18n from '../i18n/i18n';
import { formatDate, formatQuantity } from './format';

/**
 * P2.5.7 — locale-aware table export to CSV.
 *
 * CSV uses the active locale's number format (so Macedonian users get
 * `1.234,56` and English users get `1,234.56`). Date columns go through
 * `formatDate` for consistency with the on-screen table. Strings are
 * quoted + escaped per RFC 4180. BOM is prefixed so Excel opens the file
 * as UTF-8 on Windows.
 */

export interface CsvColumn<T> {
  key: keyof T | string;
  label: string;
  type?: 'text' | 'number' | 'date';
  decimals?: number;
  /** Custom accessor if the column doesn't map to a flat property. */
  get?: (row: T) => unknown;
}

function quote(raw: unknown): string {
  if (raw === null || raw === undefined) return '';
  const s = String(raw);
  if (/[",\n;]/.test(s)) {
    return `"${s.replace(/"/g, '""')}"`;
  }
  return s;
}

function formatCell<T>(row: T, col: CsvColumn<T>): string {
  const raw = col.get ? col.get(row) : (row as unknown as Record<string, unknown>)[col.key as string];
  if (raw === null || raw === undefined) return '';
  if (col.type === 'number') {
    const n = Number(raw);
    if (Number.isNaN(n)) return '';
    return quote(formatQuantity(n, col.decimals ?? 2));
  }
  if (col.type === 'date') {
    return quote(formatDate(raw as string));
  }
  return quote(raw);
}

export function exportToCsv<T>(rows: T[], columns: CsvColumn<T>[], filename: string): void {
  const header = columns.map((c) => quote(c.label)).join(',');
  const body = rows.map((r) => columns.map((c) => formatCell(r, c)).join(',')).join('\n');
  const csv = '\uFEFF' + header + '\n' + body; // BOM for Excel UTF-8 detection
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  const ts = new Date().toISOString().replace(/[:.]/g, '-').substring(0, 19);
  a.download = `${filename}-${ts}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

/** Resolve a translated label for a column via its i18n key. */
export function translatedLabel(key: string, fallback: string): string {
  const t = i18n.t(key);
  return t && t !== key ? t : fallback;
}
