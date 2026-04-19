import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { importApi } from '../services/api';

/* ------------ shape types (manually mirrored; not codegen'd to stay lean) ------------ */

type Scope = 1 | 2 | 3; // Row=1, Header=2, Either=3
type FieldType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8; // String..Enum

interface TargetField {
  name: string;
  label: string;
  type: FieldType;
  required: boolean;
  scope: Scope;
  enumValues?: string[] | null;
  lookupEntity?: string | null;
  lookupField?: string | null;
  notes?: string | null;
}

interface TargetSchema {
  targetName: string;
  displayLabel: string;
  fields: TargetField[];
}

interface Session {
  id: string;
  originalFileName: string;
  format: number;
  fileSizeBytes: number;
  status: number;
  headers: string[];
  previewRows: (string | null)[][];
  totalRowCount: number;
  targetEntity: string | null;
  partnerContextId: string | null;
  mapping?: { columns: MappingColumn[] } | null;
  defaults?: { values: Record<string, string | null | undefined> } | null;
  transforms?: { columns: TransformColumn[] } | null;
}

interface MappingColumn {
  sourceHeader: string;
  targetField: string | null;
  ignore: boolean;
}
interface TransformColumn {
  sourceHeader: string;
  rules: string[];
}

interface MappingProfile {
  id: string;
  label: string;
  targetEntity: string;
  partnerContextId: string | null;
  mapping: { columns: MappingColumn[] };
  usageCount: number;
  lastUsedAt: string | null;
}

interface RunReport {
  targetEntity: string;
  rowCount: number;
  rowsWithErrors: number;
  rows: Array<{ rowIndex: number; errors: string[]; warnings: string[] }>;
  committable: boolean;
  wasCommitted: boolean;
  entitiesCreated: number;
}

type Step = 'upload' | 'mapping' | 'defaults' | 'transforms' | 'run';

const TRANSFORM_OPTIONS = ['TRIM', 'UPPER', 'LOWER', 'DECIMAL_COMMA_TO_DOT'];

const ImportWizard: React.FC = () => {
  const { t } = useTranslation();
  const [step, setStep] = useState<Step>('upload');
  const [session, setSession] = useState<Session | null>(null);
  const [targets, setTargets] = useState<TargetSchema[]>([]);
  const [currentTarget, setCurrentTarget] = useState<TargetSchema | null>(null);
  const [mapping, setMapping] = useState<MappingColumn[]>([]);
  const [defaults, setDefaults] = useState<Record<string, string>>({});
  const [transforms, setTransforms] = useState<TransformColumn[]>([]);
  const [transformedPreview, setTransformedPreview] = useState<(string | null)[][] | null>(null);
  const [profiles, setProfiles] = useState<MappingProfile[]>([]);
  const [profileLabel, setProfileLabel] = useState('');
  const [partnerContext, setPartnerContext] = useState<string>('');
  const [runReport, setRunReport] = useState<RunReport | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const fileInput = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    importApi.getTargets().then((r) => setTargets(r.data?.data || [])).catch(() => {});
  }, []);

  const resetAll = () => {
    setStep('upload');
    setSession(null);
    setCurrentTarget(null);
    setMapping([]);
    setDefaults({});
    setTransforms([]);
    setTransformedPreview(null);
    setProfiles([]);
    setProfileLabel('');
    setPartnerContext('');
    setRunReport(null);
    setErr(null);
    if (fileInput.current) fileInput.current.value = '';
  };

  const handleFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (!f) return;
    setErr(null);
    setLoading(true);
    try {
      const r = await importApi.uploadSession(f);
      const s: Session = r.data?.data;
      setSession(s);
      // Seed mapping from file headers so user can just tweak.
      setMapping(s.headers.map((h) => ({ sourceHeader: h, targetField: null, ignore: false })));
      setStep('mapping');
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || e?.message || 'Upload failed');
    } finally {
      setLoading(false);
    }
  };

  const pickTarget = async (name: string) => {
    if (!session) return;
    try {
      const r = await importApi.getTarget(name);
      const schema: TargetSchema = r.data?.data;
      setCurrentTarget(schema);
      // Auto-match columns whose header (case-insensitive) matches a target field.
      setMapping((prev) =>
        prev.map((c) => {
          if (c.targetField) return c;
          const match = schema.fields.find(
            (f) => f.name.toLowerCase() === c.sourceHeader.toLowerCase(),
          );
          return match ? { ...c, targetField: match.name } : c;
        }),
      );
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || 'Target load failed');
    }
    // Partner-scoped profile suggestions too.
    try {
      const r = await importApi.suggestProfiles(name, partnerContext || undefined);
      setProfiles(r.data?.data || []);
    } catch {
      setProfiles([]);
    }
  };

  const applyProfile = (p: MappingProfile) => {
    setMapping(
      session?.headers.map((h) => {
        const existing = p.mapping.columns.find(
          (c) => c.sourceHeader.toLowerCase() === h.toLowerCase(),
        );
        return existing
          ? { sourceHeader: h, targetField: existing.targetField, ignore: existing.ignore }
          : { sourceHeader: h, targetField: null, ignore: false };
      }) || [],
    );
  };

  const saveMapping = async () => {
    if (!session || !currentTarget) return;
    setErr(null);
    setLoading(true);
    try {
      await importApi.applyMapping(session.id, {
        mapping: { columns: mapping },
        targetEntity: currentTarget.targetName,
        partnerContextId: partnerContext || null,
        saveAsProfileLabel: profileLabel.trim() || null,
      });
      setStep('defaults');
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || 'Mapping apply failed');
    } finally {
      setLoading(false);
    }
  };

  const saveDefaults = async () => {
    if (!session) return;
    setErr(null);
    setLoading(true);
    try {
      await importApi.setDefaults(session.id, { values: defaults });
      setStep('transforms');
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || 'Defaults save failed');
    } finally {
      setLoading(false);
    }
  };

  const saveTransforms = async () => {
    if (!session) return;
    setErr(null);
    setLoading(true);
    try {
      await importApi.setTransforms(session.id, { columns: transforms });
      const p = await importApi.previewTransformed(session.id, 20);
      setTransformedPreview((p.data?.data?.rows as (string | null)[][]) || null);
      setStep('run');
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || 'Transforms save failed');
    } finally {
      setLoading(false);
    }
  };

  const runDry = async () => {
    if (!session) return;
    setLoading(true);
    setErr(null);
    try {
      const r = await importApi.dryRun(session.id);
      setRunReport(r.data?.data);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || 'Dry-run failed');
    } finally {
      setLoading(false);
    }
  };

  const runCommit = async () => {
    if (!session) return;
    setLoading(true);
    setErr(null);
    try {
      const r = await importApi.commit(session.id);
      setRunReport(r.data?.data);
    } catch (e: any) {
      setErr(e?.response?.data?.errorMessage || 'Commit failed');
    } finally {
      setLoading(false);
    }
  };

  const mappedFields = useMemo(
    () => new Set(mapping.filter((c) => !c.ignore && c.targetField).map((c) => c.targetField!)),
    [mapping],
  );
  const missingRequired = useMemo(
    () =>
      currentTarget
        ? currentTarget.fields.filter(
            (f) => f.required && !mappedFields.has(f.name) && !defaults[f.name],
          )
        : [],
    [currentTarget, mappedFields, defaults],
  );

  return (
    <div style={{ padding: 20, maxWidth: 1100 }}>
      <h2>{t('import.title')}</h2>
      <p style={{ color: '#666', marginTop: -6 }}>{t('import.subtitle')}</p>

      <StepBar step={step} />

      {err && (
        <div style={{ color: '#b00020', background: '#fde', padding: 10, borderRadius: 4, margin: '8px 0' }}>
          {err}
        </div>
      )}

      {step === 'upload' && (
        <div style={{ marginTop: 20 }}>
          <p>{t('import.uploadHint')}</p>
          <input
            ref={fileInput}
            type="file"
            accept=".xlsx,.xls,.csv,.tsv,.json,.xml"
            onChange={handleFile}
            disabled={loading}
          />
          {loading && <p>{t('common.loading')}</p>}
        </div>
      )}

      {session && step !== 'upload' && (
        <div style={{ marginTop: 14, marginBottom: 14, padding: 10, background: '#f6f6f6', borderRadius: 4 }}>
          <strong>{t('import.session')}:</strong> {session.originalFileName} — {session.totalRowCount}{' '}
          {t('import.totalRows')} &middot;{' '}
          <button onClick={resetAll} className="secondary-button">
            {t('import.reset')}
          </button>
        </div>
      )}

      {step === 'mapping' && session && (
        <div>
          <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end' }}>
            <label>
              {t('import.target')}:
              <select
                value={currentTarget?.targetName || ''}
                onChange={(e) => pickTarget(e.target.value)}
                style={{ marginLeft: 8 }}
              >
                <option value="">-- {t('import.pickTarget')} --</option>
                {targets.map((tg) => (
                  <option key={tg.targetName} value={tg.targetName}>
                    {t(`import.targets.${tg.targetName}`, tg.displayLabel)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('import.partnerContext')}:
              <input
                type="text"
                value={partnerContext}
                onChange={(e) => setPartnerContext(e.target.value)}
                placeholder="partner GUID"
                style={{ marginLeft: 8, width: 270 }}
              />
            </label>
          </div>

          {currentTarget && profiles.length > 0 && (
            <div style={{ marginTop: 10, padding: 10, background: '#eef7ff', borderRadius: 4 }}>
              <strong>{t('import.suggestedProfiles')}:</strong>
              <ul>
                {profiles.map((p) => (
                  <li key={p.id} style={{ margin: '4px 0' }}>
                    <span>
                      {p.label} {p.partnerContextId ? '(partner-scoped)' : '(tenant-wide)'} — {p.usageCount}×
                    </span>
                    <button
                      onClick={() => applyProfile(p)}
                      className="secondary-button"
                      style={{ marginLeft: 12 }}
                    >
                      {t('import.applyProfile')}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {currentTarget && (
            <>
              <table style={{ marginTop: 16, width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr>
                    <th style={th}>{t('import.mappingTable.source')}</th>
                    <th style={th}>{t('import.mappingTable.target')}</th>
                    <th style={th}>{t('import.mappingTable.ignore')}</th>
                  </tr>
                </thead>
                <tbody>
                  {mapping.map((col, idx) => (
                    <tr key={col.sourceHeader}>
                      <td style={td}>{col.sourceHeader}</td>
                      <td style={td}>
                        <select
                          value={col.targetField || ''}
                          disabled={col.ignore}
                          onChange={(e) => {
                            const v = e.target.value || null;
                            setMapping((prev) =>
                              prev.map((c, i) => (i === idx ? { ...c, targetField: v } : c)),
                            );
                          }}
                        >
                          <option value="">—</option>
                          {currentTarget.fields.map((f) => (
                            <option key={f.name} value={f.name}>
                              {f.label}
                              {f.required ? ' *' : ''}
                              {f.scope === 2 ? ' (header)' : f.scope === 1 ? ' (row)' : ''}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td style={td}>
                        <input
                          type="checkbox"
                          checked={col.ignore}
                          onChange={(e) => {
                            const v = e.target.checked;
                            setMapping((prev) =>
                              prev.map((c, i) =>
                                i === idx ? { ...c, ignore: v, targetField: v ? null : c.targetField } : c,
                              ),
                            );
                          }}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <div style={{ marginTop: 12 }}>
                <label>
                  {t('import.saveProfileLabel')}:
                  <input
                    type="text"
                    value={profileLabel}
                    onChange={(e) => setProfileLabel(e.target.value)}
                    placeholder="(optional)"
                    style={{ marginLeft: 8, width: 260 }}
                  />
                </label>
                <button onClick={saveMapping} disabled={loading || !currentTarget} style={{ marginLeft: 12 }}>
                  {t('import.applyMapping')}
                </button>
              </div>
            </>
          )}
        </div>
      )}

      {step === 'defaults' && currentTarget && session && (
        <div>
          <p>{t('import.defaultsHint')}</p>
          {currentTarget.fields
            .filter((f) => f.scope !== 1 /* Row-only excluded */ && !mappedFields.has(f.name))
            .map((f) => (
              <div key={f.name} style={{ marginBottom: 8 }}>
                <label>
                  <strong>{f.label}</strong>
                  {f.required ? ' *' : ''}:
                  {f.enumValues ? (
                    <select
                      value={defaults[f.name] || ''}
                      onChange={(e) => setDefaults({ ...defaults, [f.name]: e.target.value })}
                      style={{ marginLeft: 8 }}
                    >
                      <option value="">—</option>
                      {f.enumValues.map((v) => (
                        <option key={v} value={v}>
                          {v}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input
                      type="text"
                      value={defaults[f.name] || ''}
                      onChange={(e) => setDefaults({ ...defaults, [f.name]: e.target.value })}
                      style={{ marginLeft: 8, width: 300 }}
                      placeholder={f.lookupEntity ? `${f.lookupEntity}.${f.lookupField}` : ''}
                    />
                  )}
                </label>
              </div>
            ))}
          {missingRequired.length > 0 && (
            <div style={{ color: '#b00020' }}>
              {t('common.required')}: {missingRequired.map((f) => f.name).join(', ')}
            </div>
          )}
          <button onClick={saveDefaults} disabled={loading}>
            {t('import.saveDefaults')}
          </button>
        </div>
      )}

      {step === 'transforms' && session && (
        <div>
          <p>{t('import.transformsHint')}</p>
          {session.headers.map((h) => {
            const existing = transforms.find((x) => x.sourceHeader === h);
            return (
              <div key={h} style={{ marginBottom: 6 }}>
                <strong>{h}</strong>:{' '}
                <input
                  type="text"
                  value={(existing?.rules || []).join(', ')}
                  onChange={(e) => {
                    const rules = e.target.value
                      .split(',')
                      .map((r) => r.trim())
                      .filter(Boolean);
                    setTransforms((prev) => {
                      const without = prev.filter((x) => x.sourceHeader !== h);
                      return rules.length === 0 ? without : [...without, { sourceHeader: h, rules }];
                    });
                  }}
                  placeholder={`e.g. ${TRANSFORM_OPTIONS.join(', ')}`}
                  style={{ width: 500, marginLeft: 6 }}
                />
              </div>
            );
          })}
          <button onClick={saveTransforms} disabled={loading}>
            {t('import.saveTransforms')}
          </button>
        </div>
      )}

      {step === 'run' && session && (
        <div>
          {transformedPreview && (
            <div style={{ margin: '10px 0' }}>
              <h4>{t('import.previewTransformed')}</h4>
              <PreviewTable headers={session.headers} rows={transformedPreview} />
            </div>
          )}
          <div style={{ display: 'flex', gap: 10, marginTop: 10 }}>
            <button onClick={runDry} disabled={loading}>
              {t('import.dryRun')}
            </button>
            <button
              onClick={runCommit}
              disabled={loading || (runReport ? !runReport.committable : false)}
              style={{ background: '#2b6cb0', color: '#fff' }}
            >
              {t('import.commit')}
            </button>
          </div>
          {runReport && (
            <div
              style={{
                marginTop: 12,
                padding: 10,
                background: runReport.wasCommitted ? '#e6ffed' : runReport.committable ? '#fff7cc' : '#fde2e1',
                borderRadius: 4,
              }}
            >
              <div>
                <strong>
                  {runReport.wasCommitted
                    ? t('import.committed')
                    : runReport.committable
                    ? t('import.committable')
                    : t('import.notCommittable')}
                </strong>
              </div>
              <div>{t('import.rowsWithErrors', { n: runReport.rowsWithErrors })}</div>
              {runReport.wasCommitted && (
                <div>{t('import.entitiesCreated', { n: runReport.entitiesCreated })}</div>
              )}
              {runReport.rowsWithErrors > 0 && (
                <details style={{ marginTop: 10 }}>
                  <summary>{t('import.rowErrors')}</summary>
                  <ul>
                    {runReport.rows
                      .filter((r) => r.errors.length > 0)
                      .slice(0, 50)
                      .map((r) => (
                        <li key={r.rowIndex}>
                          #{r.rowIndex}: {r.errors.join('; ')}
                        </li>
                      ))}
                  </ul>
                </details>
              )}
            </div>
          )}
        </div>
      )}

      {session && (step === 'mapping' || step === 'defaults') && (
        <div style={{ marginTop: 20 }}>
          <h4>{t('import.previewRows', { n: Math.min(20, session.totalRowCount) })}</h4>
          <PreviewTable headers={session.headers} rows={session.previewRows} />
        </div>
      )}
    </div>
  );
};

const StepBar: React.FC<{ step: Step }> = ({ step }) => {
  const { t } = useTranslation();
  const order: Step[] = ['upload', 'mapping', 'defaults', 'transforms', 'run'];
  return (
    <div style={{ display: 'flex', gap: 10, margin: '12px 0' }}>
      {order.map((s) => (
        <span
          key={s}
          style={{
            padding: '4px 10px',
            borderRadius: 4,
            background: s === step ? '#2b6cb0' : '#eee',
            color: s === step ? '#fff' : '#333',
          }}
        >
          {t(`import.steps.${s}`)}
        </span>
      ))}
    </div>
  );
};

const PreviewTable: React.FC<{ headers: string[]; rows: (string | null)[][] }> = ({ headers, rows }) => (
  <div style={{ overflowX: 'auto' }}>
    <table style={{ borderCollapse: 'collapse', fontSize: 13 }}>
      <thead>
        <tr>
          {headers.map((h) => (
            <th key={h} style={th}>
              {h}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((r, i) => (
          <tr key={i}>
            {headers.map((_, c) => (
              <td key={c} style={td}>
                {r[c] ?? ''}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);

const th: React.CSSProperties = { border: '1px solid #ddd', padding: '6px 10px', background: '#fafafa', textAlign: 'left' };
const td: React.CSSProperties = { border: '1px solid #eee', padding: '6px 10px' };

export default ImportWizard;
