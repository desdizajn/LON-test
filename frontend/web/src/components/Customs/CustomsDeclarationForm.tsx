import React, { useState, useEffect } from 'react';
import { customsApi, masterDataApi } from '../../services/api';

interface CustomsDeclarationFormProps {
  declaration?: any;
  onSuccess: () => void;
  onCancel: () => void;
}

const CustomsDeclarationForm: React.FC<CustomsDeclarationFormProps> = ({
  declaration,
  onSuccess,
  onCancel,
}) => {
  const [formData, setFormData] = useState({
    declarationNumber: '',
    mrn: '',
    customsProcedureId: '',
    declarationDate: new Date().toISOString().split('T')[0],
    importerId: '',
    exporterId: '',
    currency: 'EUR',
    totalCustomsValue: 0,
    totalDuty: 0,
    totalVAT: 0,
    isCleared: false,
    notes: ''
  });

  const [partners, setPartners] = useState<any[]>([]);
  const [procedures, setProcedures] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
    if (declaration) {
      setFormData({
        declarationNumber: declaration.declarationNumber || '',
        mrn: declaration.mrn || '',
        customsProcedureId: declaration.customsProcedureId || '',
        declarationDate: declaration.declarationDate?.split('T')[0] || new Date().toISOString().split('T')[0],
        importerId: declaration.importerId || '',
        exporterId: declaration.exporterId || '',
        currency: declaration.currency || 'EUR',
        totalCustomsValue: declaration.totalCustomsValue || 0,
        totalDuty: declaration.totalDuty || 0,
        totalVAT: declaration.totalVAT || 0,
        isCleared: declaration.isCleared || false,
        notes: declaration.notes || ''
      });
    }
  }, [declaration]);

  const loadData = async () => {
    try {
      const [partnersRes, proceduresRes] = await Promise.all([
        masterDataApi.getPartners(),
        customsApi.getProcedures()
      ]);
      setPartners(partnersRes.data);
      setProcedures(proceduresRes.data);
    } catch (err) {
      console.error('Failed to load data', err);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      if (declaration) {
        await customsApi.updateDeclaration(declaration.id, formData);
      } else {
        await customsApi.createDeclaration(formData);
      }
      onSuccess();
    } catch (err) {
      console.error('Failed to save declaration', err);
      alert('Failed to save declaration');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div className="header">
        <h2>{declaration ? 'Измени декларација' : 'Нова царинска декларација'}</h2>
        <button type="button" onClick={onCancel} className="secondary-button">
          Назад
        </button>
      </div>

      <form onSubmit={handleSubmit} className="form-card">
        <div className="form-grid">
          <div className="form-group">
            <label>Декларациски број *</label>
            <input
              type="text"
              value={formData.declarationNumber}
              onChange={(e) => setFormData({ ...formData, declarationNumber: e.target.value })}
              required
            />
          </div>

          <div className="form-group">
            <label>MRN</label>
            <input
              type="text"
              value={formData.mrn}
              onChange={(e) => setFormData({ ...formData, mrn: e.target.value })}
              placeholder="Ќе биде доделено"
            />
          </div>

          <div className="form-group">
            <label>Царинска процедура *</label>
            <select
              value={formData.customsProcedureId}
              onChange={(e) => setFormData({ ...formData, customsProcedureId: e.target.value })}
              required
            >
              <option value="">Избери...</option>
              {procedures.map((proc) => (
                <option key={proc.id} value={proc.id}>
                  {proc.code} - {proc.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label>Датум *</label>
            <input
              type="date"
              value={formData.declarationDate}
              onChange={(e) => setFormData({ ...formData, declarationDate: e.target.value })}
              required
            />
          </div>

          <div className="form-group">
            <label>Увозник</label>
            <select
              value={formData.importerId}
              onChange={(e) => setFormData({ ...formData, importerId: e.target.value })}
            >
              <option value="">Избери...</option>
              {partners.filter(p => p.partnerType === 1).map((partner) => (
                <option key={partner.id} value={partner.id}>
                  {partner.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label>Извозник</label>
            <select
              value={formData.exporterId}
              onChange={(e) => setFormData({ ...formData, exporterId: e.target.value })}
            >
              <option value="">Избери...</option>
              {partners.filter(p => p.partnerType === 0).map((partner) => (
                <option key={partner.id} value={partner.id}>
                  {partner.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label>Валута *</label>
            <select
              value={formData.currency}
              onChange={(e) => setFormData({ ...formData, currency: e.target.value })}
            >
              <option value="EUR">EUR</option>
              <option value="USD">USD</option>
              <option value="MKD">MKD</option>
            </select>
          </div>

          <div className="form-group">
            <label>Царинска вредност</label>
            <input
              type="number"
              step="0.01"
              value={formData.totalCustomsValue}
              onChange={(e) => setFormData({ ...formData, totalCustomsValue: parseFloat(e.target.value) || 0 })}
            />
          </div>

          <div className="form-group">
            <label>Вкупна царина</label>
            <input
              type="number"
              step="0.01"
              value={formData.totalDuty}
              onChange={(e) => setFormData({ ...formData, totalDuty: parseFloat(e.target.value) || 0 })}
            />
          </div>

          <div className="form-group">
            <label>Вкупен ДДВ</label>
            <input
              type="number"
              step="0.01"
              value={formData.totalVAT}
              onChange={(e) => setFormData({ ...formData, totalVAT: parseFloat(e.target.value) || 0 })}
            />
          </div>

          <div className="form-group">
            <label>
              <input
                type="checkbox"
                checked={formData.isCleared}
                onChange={(e) => setFormData({ ...formData, isCleared: e.target.checked })}
              />
              {' '}Оцаринето
            </label>
          </div>

          <div className="form-group full-width">
            <label>Забелешки</label>
            <textarea
              value={formData.notes}
              onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
              rows={3}
            />
          </div>
        </div>

        <div className="form-actions">
          <button type="button" onClick={onCancel} className="secondary-button">
            Откажи
          </button>
          <button type="submit" className="primary-button" disabled={loading}>
            {loading ? 'Зачувување...' : declaration ? 'Зачувај' : 'Креирај'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default CustomsDeclarationForm;
