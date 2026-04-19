import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../services/api';
import CustomsDeclarationForm from '../components/Customs/CustomsDeclarationForm';
import CertifyDeclarationModal from '../components/Customs/CertifyDeclarationModal';
import Pee060Panel from '../components/Customs/Pee060Panel';
import WasteDeclarationModal from '../components/Customs/WasteDeclarationModal';

const Customs: React.FC = () => {
  const { t } = useTranslation();
  const [declarations, setDeclarations] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showDeclarationForm, setShowDeclarationForm] = useState(false);
  const [editingDeclaration, setEditingDeclaration] = useState<any>(null);
  const [certifyTarget, setCertifyTarget] = useState<any>(null);
  const [showPee, setShowPee] = useState(false);
  const [showWaste, setShowWaste] = useState(false);

  useEffect(() => {
    loadDeclarations();
  }, []);

  const loadDeclarations = async () => {
    try {
      setLoading(true);
      const response = await customsApi.getDeclarations();
      setDeclarations(response.data);
    } catch (err) {
      console.error('Failed to load declarations', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateNew = () => {
    setEditingDeclaration(null);
    setShowDeclarationForm(true);
  };

  const handleEdit = (declaration: any) => {
    setEditingDeclaration(declaration);
    setShowDeclarationForm(true);
  };

  const handleFormSuccess = () => {
    setShowDeclarationForm(false);
    setEditingDeclaration(null);
    loadDeclarations();
  };

  const handleFormCancel = () => {
    setShowDeclarationForm(false);
    setEditingDeclaration(null);
  };

  if (showDeclarationForm) {
    return (
      <CustomsDeclarationForm
        declaration={editingDeclaration}
        onSuccess={handleFormSuccess}
        onCancel={handleFormCancel}
      />
    );
  }

  if (loading) return <div className="loading">Loading customs declarations...</div>;

  return (
    <div>
      <div className="header">
        <h2>🛃 Customs & MRN</h2>
        <div>
          <button className="btn" style={{ marginRight: 10 }} onClick={() => setShowWaste(true)}>
            + {t('waste.title')}
          </button>
          <button className="btn" style={{ marginRight: 10 }} onClick={() => setShowPee(true)}>
            {t('pee.pee060')}
          </button>
          <button className="btn btn-success" onClick={handleCreateNew}>
            + New Declaration
          </button>
        </div>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Declaration Number</th>
              <th>MRN</th>
              <th>Procedure</th>
              <th>Customs Value</th>
              <th>Total Duty</th>
              <th>Total VAT</th>
              <th>Status</th>
              <th>Date</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {declarations.map((decl, idx) => (
              <tr key={idx}>
                <td><strong>{decl.declarationNumber}</strong></td>
                <td>{decl.mrn || '-'}</td>
                <td>{decl.customsProcedure?.name || '-'}</td>
                <td>{decl.totalCustomsValue?.toFixed(2) || '0.00'} {decl.currency}</td>
                <td>{decl.totalDuty?.toFixed(2) || '0.00'}</td>
                <td>{decl.totalVAT?.toFixed(2) || '0.00'}</td>
                <td>
                  <span className={`badge badge-${decl.isCleared ? 'success' : 'warning'}`}>
                    {decl.isCleared ? 'Cleared' : 'Pending'}
                  </span>
                </td>
                <td>
                  {new Date(decl.declarationDate).toLocaleDateString()}
                  {decl.zaverkaNumber && (
                    <div style={{ fontSize: 11, color: '#0a7c36', marginTop: 2 }}>
                      ✓ {t('zaverka.certified')}: {decl.zaverkaNumber}
                    </div>
                  )}
                </td>
                <td>
                  <button onClick={() => handleEdit(decl)} className="btn-edit">Edit</button>
                  {!decl.isCleared && decl.status !== 99 && (
                    <button
                      onClick={() => setCertifyTarget(decl)}
                      className="btn"
                      style={{ marginLeft: 5 }}
                    >
                      {t('zaverka.certify')}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {certifyTarget && (
        <CertifyDeclarationModal
          declarationId={certifyTarget.id}
          declarationNumber={certifyTarget.declarationNumber}
          onClose={() => setCertifyTarget(null)}
          onSuccess={() => {
            setCertifyTarget(null);
            loadDeclarations();
          }}
        />
      )}

      {showPee && <Pee060Panel onClose={() => setShowPee(false)} />}
      {showWaste && (
        <WasteDeclarationModal
          onClose={() => setShowWaste(false)}
          onSuccess={() => {
            setShowWaste(false);
            loadDeclarations();
          }}
        />
      )}
    </div>
  );
};

export default Customs;
