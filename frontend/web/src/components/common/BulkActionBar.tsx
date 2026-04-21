import React from 'react';
import { useTranslation } from 'react-i18next';

export interface BulkAction {
  key: string;
  label: string;
  icon?: React.ReactNode;
  variant?: 'default' | 'primary' | 'danger';
  onClick: () => void;
  disabled?: boolean;
}

interface Props {
  selectedCount: number;
  totalCount?: number;
  actions: BulkAction[];
  onClearSelection: () => void;
  /** Optional summary e.g. „430.5 M selected" */
  summary?: string;
}

/**
 * Sticky bar that appears when rows are selected. Renders per-action buttons
 * plus a clear-selection control. Used together with a checkbox column +
 * useRowSelection hook.
 */
const BulkActionBar: React.FC<Props> = ({ selectedCount, totalCount, actions, onClearSelection, summary }) => {
  const { t } = useTranslation();
  if (selectedCount === 0) return null;

  const btnStyle = (variant: BulkAction['variant']): React.CSSProperties => {
    const base: React.CSSProperties = {
      padding: '6px 12px',
      borderRadius: 6,
      border: '1px solid transparent',
      cursor: 'pointer',
      fontSize: 13,
      fontWeight: 500,
    };
    if (variant === 'primary') {
      return {
        ...base,
        background: 'var(--taris-blue-500, #1e88e5)',
        color: 'white',
        borderColor: 'var(--taris-blue-500, #1e88e5)',
      };
    }
    if (variant === 'danger') {
      return {
        ...base,
        background: 'var(--taris-red-500, #e53935)',
        color: 'white',
        borderColor: 'var(--taris-red-500, #e53935)',
      };
    }
    return {
      ...base,
      background: 'white',
      color: 'var(--ink-900, #0f172a)',
      borderColor: 'var(--border-strong, #d1d5db)',
    };
  };

  return (
    <div
      style={{
        position: 'sticky',
        top: 0,
        zIndex: 20,
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        flexWrap: 'wrap',
        padding: '10px 14px',
        marginBottom: 10,
        background: 'var(--taris-blue-50, #e7f2fe)',
        border: '1px solid var(--taris-blue-200, #bedcfa)',
        borderRadius: 6,
      }}
    >
      <strong style={{ color: 'var(--taris-blue-700, #1468b6)' }}>
        {t('bulkActions.selected', { count: selectedCount, total: totalCount ?? '' })}
      </strong>
      {summary && <span style={{ color: '#555', fontSize: 13 }}>{summary}</span>}
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginLeft: 'auto' }}>
        {actions.map((a) => (
          <button
            key={a.key}
            type="button"
            onClick={a.onClick}
            disabled={a.disabled}
            style={btnStyle(a.variant)}
          >
            {a.icon && <span style={{ marginRight: 4 }}>{a.icon}</span>}
            {a.label}
          </button>
        ))}
        <button
          type="button"
          onClick={onClearSelection}
          style={{
            padding: '6px 10px',
            borderRadius: 6,
            border: '1px solid var(--border-strong, #d1d5db)',
            background: 'white',
            cursor: 'pointer',
            fontSize: 13,
          }}
        >
          {t('bulkActions.clear')}
        </button>
      </div>
    </div>
  );
};

export default BulkActionBar;
