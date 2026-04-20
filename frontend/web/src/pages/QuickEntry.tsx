import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../services/api';

/**
 * P5.2.8 — single-line command bar.
 *
 * Drops a text input at /tools/quick-entry. Power users type:
 *   issue PO-123
 *   release PO-456
 *   move BATCH-7 production
 *   help
 *
 * Sends to POST /api/QuickEntry/execute; renders outcome + any structured
 * payload returned by the dispatch target.
 */

type ExecuteResult = {
  verb: string;
  outcome: string;
  payload?: any;
};

type ResultEnvelope = {
  isSuccess: boolean;
  data: ExecuteResult | null;
  errorMessage?: string | null;
  errors?: string[];
};

type LogEntry = { id: number; cmd: string; envelope: ResultEnvelope };

const QuickEntry: React.FC = () => {
  const { t } = useTranslation();
  const [cmd, setCmd] = useState('');
  const [busy, setBusy] = useState(false);
  const [log, setLog] = useState<LogEntry[]>([]);
  const [cursor, setCursor] = useState<number | null>(null);

  const history = log.map((l) => l.cmd);

  const execute = async () => {
    if (!cmd.trim() || busy) return;
    setBusy(true);
    try {
      const r = await api.post('/QuickEntry/execute', { command: cmd });
      setLog((prev) => [
        { id: Date.now(), cmd, envelope: r.data as ResultEnvelope },
        ...prev,
      ]);
      setCmd('');
      setCursor(null);
    } catch (e: any) {
      setLog((prev) => [
        {
          id: Date.now(),
          cmd,
          envelope: {
            isSuccess: false,
            data: null,
            errorMessage: e?.response?.data?.errorMessage || e?.message || 'Failed',
          },
        },
        ...prev,
      ]);
    } finally {
      setBusy(false);
    }
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      execute();
      return;
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (history.length === 0) return;
      const next = cursor === null ? 0 : Math.min(cursor + 1, history.length - 1);
      setCursor(next);
      setCmd(history[next]);
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (cursor === null) return;
      const next = cursor - 1;
      if (next < 0) {
        setCursor(null);
        setCmd('');
      } else {
        setCursor(next);
        setCmd(history[next]);
      }
    }
  };

  return (
    <div style={{ padding: 20, maxWidth: 960, margin: '0 auto' }}>
      <h2>⚡ {t('quickEntry.title', 'Quick-entry bar')}</h2>
      <p style={{ color: '#666', marginTop: -6 }}>
        {t(
          'quickEntry.hint',
          'Еднорединска команда за power users. Верби: issue, release, move, help. ↑/↓ за историја.',
        )}
      </p>

      <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        <span style={{ fontFamily: 'monospace', fontSize: 18, opacity: 0.5 }}>›</span>
        <input
          type="text"
          value={cmd}
          onChange={(e) => setCmd(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="issue PO-123"
          autoFocus
          disabled={busy}
          style={{ flex: 1, padding: 8, fontFamily: 'monospace', fontSize: 14 }}
        />
        <button onClick={execute} disabled={busy || !cmd.trim()} style={{ padding: '8px 16px' }}>
          {busy ? '…' : t('quickEntry.run', 'Run')}
        </button>
      </div>

      <div style={{ marginTop: 20 }}>
        {log.map((entry) => (
          <div
            key={entry.id}
            style={{
              borderLeft: `3px solid ${entry.envelope.isSuccess ? '#2e7d32' : '#b00020'}`,
              padding: '8px 12px',
              marginBottom: 8,
              background: '#f7f7f7',
              borderRadius: 4,
            }}
          >
            <div style={{ fontFamily: 'monospace', fontSize: 13, color: '#555' }}>› {entry.cmd}</div>
            {entry.envelope.isSuccess ? (
              <div style={{ marginTop: 4 }}>
                <strong style={{ color: '#2e7d32' }}>✔</strong> {entry.envelope.data?.outcome}
                {entry.envelope.data?.payload && (
                  <pre style={{ fontSize: 11, marginTop: 4, background: '#fff', padding: 6 }}>
                    {JSON.stringify(entry.envelope.data.payload, null, 2)}
                  </pre>
                )}
              </div>
            ) : (
              <div style={{ marginTop: 4, color: '#b00020' }}>✘ {entry.envelope.errorMessage}</div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};

export default QuickEntry;
