import React, { useState, useEffect, useCallback } from 'react';
import { customsApi, masterDataApi, knowledgeBaseApi } from '../../services/api';

// ---------- Types ----------

interface DeclarationLine {
  key: number;
  itemId: string;
  tariffCode: string;
  quantity: number;
  uoMId: string;
  customsValue: number;
  countryOfOrigin: string;
  dutyRate: number;
  vatRate: number;
  dutyAmount: number;
  vatAmount: number;
  tariffCodeError: string;
  tariffCodeInfo: string;
}

interface FieldErrors {
  declarationNumber?: string;
  customsProcedureId?: string;
  lines?: string;
  [key: string]: string | undefined;
}

interface CustomsDeclarationFormProps {
  declaration?: any;
  onSuccess: () => void;
  onCancel: () => void;
}

// ---------- Helpers ----------

let lineKeyCounter = 0;
const nextLineKey = () => ++lineKeyCounter;

const emptyLine = (): DeclarationLine => ({
  key: nextLineKey(),
  itemId: '',
  tariffCode: '',
  quantity: 0,
  uoMId: '',
  customsValue: 0,
  countryOfOrigin: '',
  dutyRate: 0,
  vatRate: 18,
  dutyAmount: 0,
  vatAmount: 0,
  tariffCodeError: '',
  tariffCodeInfo: '',
});

const calcLine = (line: DeclarationLine): DeclarationLine => {
  const dutyAmount = Math.round(line.customsValue * line.dutyRate) / 100;
  const vatAmount = Math.round((line.customsValue + dutyAmount) * line.vatRate) / 100;
  return { ...line, dutyAmount, vatAmount };
};

const round2 = (n: number) => Math.round(n * 100) / 100;

// ---------- Status badge ----------
const STATUS_LABELS: Record<number, { label: string; color: string }> = {
  0: { label: 'Draft', color: '#7f8c8d' },
  1: { label: 'Registered', color: '#3498db' },
  2: { label: 'Submitted', color: '#f39c12' },
  3: { label: 'Cleared', color: '#27ae60' },
  99: { label: 'Cancelled', color: '#c0392b' },
};

const StatusBadge: React.FC<{ status: number }> = ({ status }) => {
  const meta = STATUS_LABELS[status] ?? { label: `#${status}`, color: '#555' };
  return (
    <span
      style={{
        background: meta.color,
        color: 'white',
        padding: '3px 10px',
        borderRadius: '12px',
        fontSize: '12px',
        fontWeight: 600,
      }}
    >
      {meta.label}
    </span>
  );
};

// ---------- Component ----------

const CustomsDeclarationForm: React.FC<CustomsDeclarationFormProps> = ({
  declaration,
  onSuccess,
  onCancel,
}) => {
  // --- Header state ---
  const [formData, setFormData] = useState({
    declarationNumber: '',
    mrn: '',
    customsProcedureId: '',
    lonAuthorizationId: '',
    declarationDate: new Date().toISOString().split('T')[0],
    partnerId: '',
    currency: 'EUR',
    dueDate: '',
    senderName: '',
    senderAddress: '',
    senderCountry: '',
    countryOfDispatch: '',
    countryOfDestination: 'MK',
  });

  // --- Lines state ---
  const [lines, setLines] = useState<DeclarationLine[]>([emptyLine()]);

  // --- Reference data ---
  const [partners, setPartners] = useState<any[]>([]);
  const [procedures, setProcedures] = useState<any[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [uoms, setUoms] = useState<any[]>([]);
  const [lonAuths, setLonAuths] = useState<any[]>([]);

  // --- UI state ---
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitError, setSubmitError] = useState('');
  const [validationSummary, setValidationSummary] = useState<string[]>([]);

  // --- Computed totals ---
  const totalCustomsValue = round2(lines.reduce((s, l) => s + (l.customsValue || 0), 0));
  const totalDuty = round2(lines.reduce((s, l) => s + l.dutyAmount, 0));
  const totalVAT = round2(lines.reduce((s, l) => s + l.vatAmount, 0));

  // --- Load reference data ---
  useEffect(() => {
    const loadData = async () => {
      try {
        const [partnersRes, proceduresRes, itemsRes, uomsRes, lonAuthsRes] = await Promise.all([
          masterDataApi.getPartners(),
          customsApi.getProcedures(),
          masterDataApi.getItems(),
          masterDataApi.getUoM(),
          customsApi.getLONAuthorizations(true),
        ]);
        setPartners(partnersRes.data);
        setProcedures(proceduresRes.data);
        setItems(itemsRes.data);
        setUoms(uomsRes.data);
        setLonAuths(lonAuthsRes.data);
      } catch (err) {
        console.error('Грешка при вчитување на податоци', err);
      }
    };
    loadData();
  }, []);

  // --- Populate when editing ---
  useEffect(() => {
    if (declaration) {
      setFormData({
        declarationNumber: declaration.declarationNumber || '',
        mrn: declaration.mrn || '',
        customsProcedureId: declaration.customsProcedureId || '',
        lonAuthorizationId: declaration.lonAuthorizationId || '',
        declarationDate: declaration.declarationDate?.split('T')[0] || new Date().toISOString().split('T')[0],
        partnerId: declaration.partnerId || '',
        currency: declaration.currency || 'EUR',
        dueDate: declaration.dueDate?.split('T')[0] || '',
        senderName: declaration.senderName || '',
        senderAddress: declaration.senderAddress || '',
        senderCountry: declaration.senderCountry || '',
        countryOfDispatch: declaration.countryOfDispatch || '',
        countryOfDestination: declaration.countryOfDestination || 'MK',
      });
      if (declaration.lines && declaration.lines.length > 0) {
        setLines(
          declaration.lines.map((dl: any) =>
            calcLine({
              key: nextLineKey(),
              itemId: dl.itemId || '',
              tariffCode: dl.tariffCode || '',
              quantity: dl.quantity || 0,
              uoMId: dl.uoMId || '',
              customsValue: dl.customsValue || 0,
              countryOfOrigin: dl.countryOfOrigin || '',
              dutyRate: dl.dutyRate || 0,
              vatRate: dl.vatRate ?? 18,
              dutyAmount: 0,
              vatAmount: 0,
              tariffCodeError: '',
              tariffCodeInfo: '',
            })
          )
        );
      }
    }
  }, [declaration]);

  // --- Header field change ---
  const handleHeaderChange = (field: string, value: string) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (errors[field]) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next[field];
        return next;
      });
    }
  };

  // --- Line field change ---
  const updateLine = useCallback((index: number, field: keyof DeclarationLine, value: any) => {
    setLines((prev) => {
      const updated = [...prev];
      const line = { ...updated[index], [field]: value } as DeclarationLine;
      updated[index] = calcLine(line);
      return updated;
    });
  }, []);

  // --- Add / remove lines ---
  const addLine = () => setLines((prev) => [...prev, emptyLine()]);

  const removeLine = (index: number) => {
    setLines((prev) => {
      if (prev.length <= 1) return prev;
      return prev.filter((_, i) => i !== index);
    });
  };

  // --- Tariff code blur validation ---
  const handleTariffCodeBlur = async (index: number) => {
    const code = lines[index].tariffCode.trim();

    // Format validation: exactly 10 digits
    if (code && !/^\d{10}$/.test(code)) {
      setLines((prev) => {
        const updated = [...prev];
        updated[index] = {
          ...updated[index],
          tariffCodeError: 'Тарифниот код мора да содржи точно 10 цифри',
          tariffCodeInfo: '',
        };
        return updated;
      });
      return;
    }

    if (!code) {
      setLines((prev) => {
        const updated = [...prev];
        updated[index] = { ...updated[index], tariffCodeError: '', tariffCodeInfo: '' };
        return updated;
      });
      return;
    }

    // Lookup tariff code via API
    try {
      const res = await knowledgeBaseApi.getTariffCodes(code);
      const data = res.data;
      const found = Array.isArray(data) ? data : data?.items || data?.data || [];

      if (found.length > 0) {
        const match = found[0];
        const dutyRate = match.dutyRate ?? match.rate ?? lines[index].dutyRate;
        const description = match.description || match.name || '';
        setLines((prev) => {
          const updated = [...prev];
          const line = {
            ...updated[index],
            dutyRate,
            tariffCodeError: '',
            tariffCodeInfo: description
              ? `${description} (Царина: ${dutyRate}%)`
              : `Царина: ${dutyRate}%`,
          };
          updated[index] = calcLine(line);
          return updated;
        });
      } else {
        setLines((prev) => {
          const updated = [...prev];
          updated[index] = {
            ...updated[index],
            tariffCodeError: 'Тарифниот код не е пронајден во базата',
            tariffCodeInfo: '',
          };
          return updated;
        });
      }
    } catch {
      setLines((prev) => {
        const updated = [...prev];
        updated[index] = {
          ...updated[index],
          tariffCodeError: 'Грешка при пребарување на тарифен код',
          tariffCodeInfo: '',
        };
        return updated;
      });
    }
  };

  // --- Full form validation ---
  const validate = (): boolean => {
    const errs: FieldErrors = {};
    const summary: string[] = [];

    if (!formData.declarationNumber.trim()) {
      errs.declarationNumber = 'Декларацискиот број е задолжителен';
      summary.push('Декларацискиот број е задолжителен');
    }
    if (!formData.senderName.trim()) {
      errs.senderName = 'Рубрика 02: Испраќач е задолжителен';
      summary.push('Рубрика 02: Испраќач / извозник е задолжителен');
    }
    if (!formData.customsProcedureId) {
      errs.customsProcedureId = 'Царинската процедура е задолжителна';
      summary.push('Царинската процедура е задолжителна');
    }

    const selectedProc = procedures.find((p) => p.id === formData.customsProcedureId);
    const procCode = (selectedProc?.code || '').toString();
    if ((procCode === '4200' || procCode === '5100') && !formData.lonAuthorizationId) {
      errs.lonAuthorizationId = `LON Одобрение е задолжително за процедура ${procCode}`;
      summary.push(`LON Одобрение е задолжително за процедура ${procCode}`);
    }

    if (lines.length === 0 || lines.every((l) => !l.itemId && !l.tariffCode)) {
      errs.lines = 'Потребна е најмалку една ставка';
      summary.push('Потребна е најмалку една ставка');
    }

    // Per-line validation
    lines.forEach((line, i) => {
      const num = i + 1;
      if (line.tariffCode && !/^\d{10}$/.test(line.tariffCode)) {
        errs[`line_${i}_tariffCode`] = 'Тарифниот код мора да содржи точно 10 цифри';
        summary.push(`Ставка ${num}: Невалиден тарифен код`);
      }
      if (line.tariffCodeError) {
        summary.push(`Ставка ${num}: ${line.tariffCodeError}`);
      }
      if (line.countryOfOrigin && (line.countryOfOrigin.length < 2 || line.countryOfOrigin.length > 3)) {
        errs[`line_${i}_countryOfOrigin`] = 'Земјата на потекло мора да содржи 2-3 карактери';
        summary.push(`Ставка ${num}: Невалидна земја на потекло`);
      }
    });

    setErrors(errs);
    setValidationSummary(summary);
    return Object.keys(errs).length === 0;
  };

  // --- Submit ---
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitError('');

    if (!validate()) return;

    const payload = {
      declarationNumber: formData.declarationNumber,
      mrn: formData.mrn || '',
      declarationDate: new Date(formData.declarationDate).toISOString(),
      customsProcedureId: formData.customsProcedureId,
      lonAuthorizationId: formData.lonAuthorizationId || null,
      partnerId: formData.partnerId || null,
      totalCustomsValue,
      currency: formData.currency,
      dueDate: formData.dueDate ? new Date(formData.dueDate).toISOString() : null,
      senderName: formData.senderName || null,
      senderAddress: formData.senderAddress || null,
      senderCountry: formData.senderCountry || null,
      countryOfDispatch: formData.countryOfDispatch || null,
      countryOfDestination: formData.countryOfDestination || null,
      lines: lines.map((l) => ({
        itemId: l.itemId,
        tariffCode: l.tariffCode,
        quantity: l.quantity,
        uoMId: l.uoMId,
        customsValue: l.customsValue,
        countryOfOrigin: l.countryOfOrigin,
        dutyRate: l.dutyRate,
        vatRate: l.vatRate,
      })),
    };

    setLoading(true);
    try {
      if (declaration) {
        await customsApi.updateDeclaration(declaration.id, payload);
      } else {
        await customsApi.createDeclaration(payload);
      }
      onSuccess();
    } catch (err: any) {
      console.error('Грешка при зачувување', err);
      setSubmitError(
        err?.response?.data?.message || 'Грешка при зачувување на декларацијата'
      );
    } finally {
      setLoading(false);
    }
  };

  // ===================== RENDER =====================

  return (
    <div>
      {/* --- Header --- */}
      <div className="header">
        <h2 style={{ display: 'inline-flex', alignItems: 'center', gap: '12px' }}>
          {declaration ? 'Измени декларација' : 'Нова царинска декларација'}
          {declaration && typeof declaration.status !== 'undefined' && (
            <StatusBadge status={declaration.status} />
          )}
        </h2>
        <button type="button" onClick={onCancel} className="btn btn-primary">
          Назад
        </button>
      </div>

      {/* --- Validation summary --- */}
      {validationSummary.length > 0 && (
        <div
          style={{
            background: '#f8d7da',
            color: '#721c24',
            padding: '15px',
            borderRadius: '8px',
            marginBottom: '20px',
          }}
        >
          <strong>Грешки при валидација:</strong>
          <ul style={{ margin: '8px 0 0 20px' }}>
            {validationSummary.map((msg, i) => (
              <li key={i}>{msg}</li>
            ))}
          </ul>
        </div>
      )}

      {submitError && (
        <div
          style={{
            background: '#f8d7da',
            color: '#721c24',
            padding: '15px',
            borderRadius: '8px',
            marginBottom: '20px',
          }}
        >
          {submitError}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        {/* ========== HEADER SECTION ========== */}
        <div
          style={{
            background: 'white',
            padding: '25px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
            marginBottom: '20px',
          }}
        >
          <h3 style={{ color: '#2c3e50', marginBottom: '20px', fontSize: '18px' }}>
            Основни податоци
          </h3>
          <div className="form-grid">
            {/* Декларациски број */}
            <div className="form-group">
              <label>Декларациски број *</label>
              <input
                type="text"
                value={formData.declarationNumber}
                onChange={(e) => handleHeaderChange('declarationNumber', e.target.value)}
                className={errors.declarationNumber ? 'error' : ''}
              />
              {errors.declarationNumber && (
                <span className="error-message">{errors.declarationNumber}</span>
              )}
            </div>

            {/* MRN */}
            <div className="form-group">
              <label>MRN</label>
              <input
                type="text"
                value={formData.mrn}
                onChange={(e) => handleHeaderChange('mrn', e.target.value)}
                placeholder="Остави празно за авто-генерирање"
              />
              {!formData.mrn && (
                <small style={{ color: '#7f8c8d', fontSize: '12px' }}>
                  Ако е празно, системот ќе генерира placeholder MRN (замени го со вистинскиот
                  од царински одговор во продукција).
                </small>
              )}
            </div>

            {/* Царинска процедура */}
            <div className="form-group">
              <label>Царинска процедура *</label>
              <select
                value={formData.customsProcedureId}
                onChange={(e) => handleHeaderChange('customsProcedureId', e.target.value)}
                className={errors.customsProcedureId ? 'error' : ''}
              >
                <option value="">Избери...</option>
                {procedures.map((proc) => (
                  <option key={proc.id} value={proc.id}>
                    {proc.code} - {proc.name}
                  </option>
                ))}
              </select>
              {errors.customsProcedureId && (
                <span className="error-message">{errors.customsProcedureId}</span>
              )}
            </div>

            {/* Датум */}
            <div className="form-group">
              <label>Датум на декларација *</label>
              <input
                type="date"
                value={formData.declarationDate}
                onChange={(e) => handleHeaderChange('declarationDate', e.target.value)}
                required
              />
            </div>

            {/* Партнер */}
            <div className="form-group">
              <label>Партнер</label>
              <select
                value={formData.partnerId}
                onChange={(e) => handleHeaderChange('partnerId', e.target.value)}
              >
                <option value="">Избери...</option>
                {partners.map((partner) => (
                  <option key={partner.id} value={partner.id}>
                    {partner.name}
                  </option>
                ))}
              </select>
            </div>

            {/* LON Одобрение — задолжително за 4200 / 5100 */}
            {(() => {
              const selectedProc = procedures.find((p) => p.id === formData.customsProcedureId);
              const procCode = (selectedProc?.code || '').toString();
              const isLonProc = procCode === '4200' || procCode === '5100';
              if (!isLonProc) return null;
              return (
                <div className="form-group">
                  <label>LON Одобрение *</label>
                  <select
                    value={formData.lonAuthorizationId}
                    onChange={(e) => handleHeaderChange('lonAuthorizationId', e.target.value)}
                    className={errors.lonAuthorizationId ? 'error' : ''}
                  >
                    <option value="">Избери Одобрение...</option>
                    {lonAuths.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.authorizationNumber}
                        {a.partnerName ? ` — ${a.partnerName}` : ''}
                        {a.expiryDate
                          ? ` (до ${a.expiryDate.split('T')[0]})`
                          : ''}
                      </option>
                    ))}
                  </select>
                  {errors.lonAuthorizationId && (
                    <span className="error-message">{errors.lonAuthorizationId}</span>
                  )}
                  <small style={{ color: '#7f8c8d', fontSize: '12px' }}>
                    Задолжително за процедура {procCode}. УСЦЗ, член 349.
                  </small>
                </div>
              );
            })()}

            {/* Валута */}
            <div className="form-group">
              <label>Валута *</label>
              <select
                value={formData.currency}
                onChange={(e) => handleHeaderChange('currency', e.target.value)}
              >
                <option value="EUR">EUR</option>
                <option value="USD">USD</option>
                <option value="MKD">MKD</option>
              </select>
            </div>

            {/* Рок за плаќање */}
            <div className="form-group">
              <label>Рок за плаќање</label>
              <input
                type="date"
                value={formData.dueDate}
                onChange={(e) => handleHeaderChange('dueDate', e.target.value)}
              />
            </div>

            {/* Рубрика 02 Sender */}
            <div className="form-group">
              <label>Рубрика 02 — Испраќач *</label>
              <input
                type="text"
                value={formData.senderName}
                onChange={(e) => handleHeaderChange('senderName', e.target.value)}
                placeholder="Име на испраќач / извозник"
                className={errors.senderName ? 'error' : ''}
              />
              {errors.senderName && <span className="error-message">{errors.senderName}</span>}
            </div>

            <div className="form-group">
              <label>Рубрика 02 — Земја на испраќач</label>
              <input
                type="text"
                maxLength={2}
                value={formData.senderCountry}
                onChange={(e) => handleHeaderChange('senderCountry', e.target.value.toUpperCase())}
                placeholder="ISO (DE, IT, CN ...)"
              />
            </div>

            {/* Рубрика 15 Country of dispatch */}
            <div className="form-group">
              <label>Рубрика 15 — Земја на испраќање</label>
              <input
                type="text"
                maxLength={2}
                value={formData.countryOfDispatch}
                onChange={(e) => handleHeaderChange('countryOfDispatch', e.target.value.toUpperCase())}
                placeholder="ISO"
              />
            </div>

            {/* Рубрика 17 Country of destination */}
            <div className="form-group">
              <label>Рубрика 17 — Земја на дестинација</label>
              <input
                type="text"
                maxLength={2}
                value={formData.countryOfDestination}
                onChange={(e) => handleHeaderChange('countryOfDestination', e.target.value.toUpperCase())}
                placeholder="MK"
              />
            </div>
          </div>
        </div>

        {/* ========== DECLARATION LINES ========== */}
        <div
          style={{
            background: 'white',
            padding: '25px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
            marginBottom: '20px',
          }}
        >
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: '20px',
            }}
          >
            <h3 style={{ color: '#2c3e50', fontSize: '18px' }}>
              Ставки на декларацијата
            </h3>
            <button
              type="button"
              onClick={addLine}
              className="btn btn-success"
            >
              + Додади ставка
            </button>
          </div>

          {errors.lines && (
            <div
              style={{
                color: '#e74c3c',
                fontSize: '13px',
                marginBottom: '15px',
              }}
            >
              {errors.lines}
            </div>
          )}

          {lines.map((line, idx) => (
            <div
              key={line.key}
              style={{
                border: '1px solid #ddd',
                borderRadius: '8px',
                padding: '20px',
                marginBottom: '15px',
                background: '#fafafa',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: '15px',
                }}
              >
                <strong style={{ color: '#2c3e50' }}>Ставка #{idx + 1}</strong>
                {lines.length > 1 && (
                  <button
                    type="button"
                    onClick={() => removeLine(idx)}
                    style={{
                      background: '#e74c3c',
                      color: 'white',
                      border: 'none',
                      borderRadius: '4px',
                      padding: '6px 14px',
                      cursor: 'pointer',
                      fontSize: '13px',
                    }}
                  >
                    Отстрани
                  </button>
                )}
              </div>

              {/* Row 1 */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '15px' }}>
                {/* Артикл */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Артикл</label>
                  <select
                    value={line.itemId}
                    onChange={(e) => updateLine(idx, 'itemId', e.target.value)}
                  >
                    <option value="">Избери...</option>
                    {items.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code ? `${item.code} - ${item.name}` : item.name}
                      </option>
                    ))}
                  </select>
                </div>

                {/* Тарифен код */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Тарифен код</label>
                  <input
                    type="text"
                    maxLength={10}
                    value={line.tariffCode}
                    onChange={(e) => {
                      const val = e.target.value.replace(/[^0-9]/g, '').slice(0, 10);
                      updateLine(idx, 'tariffCode', val);
                    }}
                    onBlur={() => handleTariffCodeBlur(idx)}
                    placeholder="0000000000"
                    className={line.tariffCodeError || errors[`line_${idx}_tariffCode`] ? 'error' : ''}
                  />
                  {line.tariffCodeError && (
                    <span className="error-message">{line.tariffCodeError}</span>
                  )}
                  {errors[`line_${idx}_tariffCode`] && !line.tariffCodeError && (
                    <span className="error-message">{errors[`line_${idx}_tariffCode`]}</span>
                  )}
                  {line.tariffCodeInfo && (
                    <span style={{ display: 'block', marginTop: '4px', fontSize: '12px', color: '#27ae60' }}>
                      {line.tariffCodeInfo}
                    </span>
                  )}
                </div>

                {/* Количина */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Количина</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={line.quantity || ''}
                    onChange={(e) => updateLine(idx, 'quantity', parseFloat(e.target.value) || 0)}
                  />
                </div>

                {/* ЕМ */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Единица мерка</label>
                  <select
                    value={line.uoMId}
                    onChange={(e) => updateLine(idx, 'uoMId', e.target.value)}
                  >
                    <option value="">Избери...</option>
                    {uoms.map((u: any) => (
                      <option key={u.id} value={u.id}>
                        {u.code ? `${u.code} - ${u.name}` : u.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Row 2 */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '15px' }}>
                {/* Царинска вредност */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Царинска вредност</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={line.customsValue || ''}
                    onChange={(e) =>
                      updateLine(idx, 'customsValue', parseFloat(e.target.value) || 0)
                    }
                  />
                </div>

                {/* Земја на потекло */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Земја на потекло</label>
                  <input
                    type="text"
                    maxLength={3}
                    value={line.countryOfOrigin}
                    onChange={(e) =>
                      updateLine(idx, 'countryOfOrigin', e.target.value.toUpperCase().slice(0, 3))
                    }
                    placeholder="MK"
                    className={errors[`line_${idx}_countryOfOrigin`] ? 'error' : ''}
                  />
                  {errors[`line_${idx}_countryOfOrigin`] && (
                    <span className="error-message">{errors[`line_${idx}_countryOfOrigin`]}</span>
                  )}
                </div>

                {/* Царинска стапка */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>Царинска стапка %</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={line.dutyRate || ''}
                    onChange={(e) =>
                      updateLine(idx, 'dutyRate', parseFloat(e.target.value) || 0)
                    }
                  />
                </div>

                {/* ДДВ стапка */}
                <div className="form-group" style={{ marginBottom: '12px' }}>
                  <label style={{ fontSize: '13px' }}>ДДВ стапка %</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={line.vatRate}
                    onChange={(e) =>
                      updateLine(idx, 'vatRate', parseFloat(e.target.value) || 0)
                    }
                  />
                </div>
              </div>

              {/* Calculated fields row */}
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(2, 1fr)',
                  gap: '15px',
                  background: '#ecf0f1',
                  padding: '12px',
                  borderRadius: '6px',
                  marginTop: '4px',
                }}
              >
                <div>
                  <label style={{ fontSize: '12px', color: '#7f8c8d', fontWeight: 600 }}>
                    Износ на царина
                  </label>
                  <div style={{ fontSize: '16px', fontWeight: 'bold', color: '#2c3e50' }}>
                    {line.dutyAmount.toFixed(2)} {formData.currency}
                  </div>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#7f8c8d', fontWeight: 600 }}>
                    Износ на ДДВ
                  </label>
                  <div style={{ fontSize: '16px', fontWeight: 'bold', color: '#2c3e50' }}>
                    {line.vatAmount.toFixed(2)} {formData.currency}
                  </div>
                </div>
              </div>
            </div>
          ))}

          {/* Add line button at bottom */}
          <button
            type="button"
            onClick={addLine}
            className="btn btn-success"
            style={{ marginTop: '5px' }}
          >
            + Додади ставка
          </button>
        </div>

        {/* ========== TOTALS ========== */}
        <div
          style={{
            background: 'white',
            padding: '25px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
            marginBottom: '20px',
          }}
        >
          <h3 style={{ color: '#2c3e50', marginBottom: '20px', fontSize: '18px' }}>
            Вкупно
          </h3>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '20px' }}>
            <div
              style={{
                background: '#ebf5fb',
                padding: '15px',
                borderRadius: '8px',
                borderLeft: '4px solid #3498db',
              }}
            >
              <div style={{ fontSize: '13px', color: '#7f8c8d', marginBottom: '5px' }}>
                Вкупна царинска вредност
              </div>
              <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#2c3e50' }}>
                {totalCustomsValue.toFixed(2)} {formData.currency}
              </div>
            </div>
            <div
              style={{
                background: '#fef9e7',
                padding: '15px',
                borderRadius: '8px',
                borderLeft: '4px solid #f39c12',
              }}
            >
              <div style={{ fontSize: '13px', color: '#7f8c8d', marginBottom: '5px' }}>
                Вкупна царина
              </div>
              <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#2c3e50' }}>
                {totalDuty.toFixed(2)} {formData.currency}
              </div>
            </div>
            <div
              style={{
                background: '#eafaf1',
                padding: '15px',
                borderRadius: '8px',
                borderLeft: '4px solid #27ae60',
              }}
            >
              <div style={{ fontSize: '13px', color: '#7f8c8d', marginBottom: '5px' }}>
                Вкупен ДДВ
              </div>
              <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#2c3e50' }}>
                {totalVAT.toFixed(2)} {formData.currency}
              </div>
            </div>
          </div>
        </div>

        {/* ========== ACTIONS ========== */}
        <div className="form-actions">
          <button type="button" onClick={onCancel} className="btn" style={{ background: '#95a5a6', color: 'white' }}>
            Откажи
          </button>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Зачувување...' : declaration ? 'Зачувај' : 'Креирај декларација'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default CustomsDeclarationForm;
