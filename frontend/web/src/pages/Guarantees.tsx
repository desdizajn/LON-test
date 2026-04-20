import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { guaranteeApi, masterDataApi } from '../services/api';
import TrafficLightGuarantees from '../components/common/TrafficLightGuarantees';
import { formatQuantity, formatDate } from '../utils/format';

interface GuaranteeAccount {
  id: string;
  accountNumber: string;
  accountName: string;
  currency: string;
  totalLimit: number;
  currentBalance: number;
  availableLimit: number;
  bankName?: string;
}

const Guarantees: React.FC = () => {
  const { t } = useTranslation();
  const [accounts, setAccounts] = useState<GuaranteeAccount[]>([]);
  const [activeGuarantees, setActiveGuarantees] = useState<any[]>([]);
  const [partners, setPartners] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAccountModal, setShowAccountModal] = useState(false);
  const [showLedgerModal, setShowLedgerModal] = useState(false);
  const [editingAccount, setEditingAccount] = useState<GuaranteeAccount | null>(null);

  const [accountFormData, setAccountFormData] = useState({
    accountNumber: '', accountName: '', bankPartnerId: '',
    currency: 'EUR', totalLimit: 0, isActive: true,
  });

  const [ledgerFormData, setLedgerFormData] = useState({
    guaranteeAccountId: '', entryType: 0, amount: 0,
    currency: 'EUR', mrn: '', description: '', expectedReleaseDate: '',
  });

  useEffect(() => { loadData(); }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [accountsRes, guaranteesRes, partnersRes] = await Promise.all([
        guaranteeApi.getAccounts(),
        guaranteeApi.getActiveGuarantees(),
        masterDataApi.getPartners(),
      ]);
      setAccounts(accountsRes.data);
      setActiveGuarantees(guaranteesRes.data);
      setPartners(partnersRes.data.filter((p: any) => p.partnerType === 2));
    } catch (err) {
      console.error('Failed to load guarantees', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateAccount = () => {
    setEditingAccount(null);
    setAccountFormData({ accountNumber: '', accountName: '', bankPartnerId: '', currency: 'EUR', totalLimit: 0, isActive: true });
    setShowAccountModal(true);
  };

  const handleEditAccount = (account: GuaranteeAccount) => {
    setEditingAccount(account);
    setAccountFormData({
      accountNumber: account.accountNumber, accountName: account.accountName,
      bankPartnerId: '', currency: account.currency, totalLimit: account.totalLimit, isActive: true,
    });
    setShowAccountModal(true);
  };

  const handleSubmitAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingAccount) await guaranteeApi.updateAccount(editingAccount.id, accountFormData);
      else await guaranteeApi.createAccount(accountFormData);
      setShowAccountModal(false);
      loadData();
    } catch (err) {
      console.error('Failed to save account', err);
      alert(t('guarantees.errors.saveAccount'));
    }
  };

  const handleCreateLedgerEntry = () => {
    setLedgerFormData({
      guaranteeAccountId: accounts.length > 0 ? accounts[0].id : '',
      entryType: 0, amount: 0, currency: 'EUR', mrn: '', description: '', expectedReleaseDate: '',
    });
    setShowLedgerModal(true);
  };

  const handleSubmitLedger = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await guaranteeApi.createLedgerEntry(ledgerFormData);
      setShowLedgerModal(false);
      loadData();
    } catch (err: any) {
      console.error('Failed to create ledger entry', err);
      alert(err.response?.data?.message || t('guarantees.errors.saveLedger'));
    }
  };

  if (loading) return <div className="loading">{t('guarantees.loading')}</div>;

  return (
    <div>
      <div className="header">
        <h2>{t('guarantees.title')}</h2>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button onClick={handleCreateAccount} className="btn-primary">
            + {t('guarantees.newAccount')}
          </button>
          <button onClick={handleCreateLedgerEntry}>
            + {t('guarantees.newLedgerEntry')}
          </button>
        </div>
      </div>

      <TrafficLightGuarantees />

      <h3 style={{ marginBottom: 12, marginTop: 24 }}>{t('guarantees.accountsTitle')}</h3>
      <div className="card-grid" style={{ marginBottom: 24 }}>
        {accounts.map((acc) => (
          <div key={acc.id} className="card" onClick={() => handleEditAccount(acc)} style={{ cursor: 'pointer' }}>
            <h3>{acc.accountName}</h3>
            <div style={{ fontSize: 12, color: 'var(--ink-500)', marginBottom: 8 }}>{acc.accountNumber}</div>
            <div style={{ fontSize: 13, marginTop: 10 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <span>{t('guarantees.totalLimit')}:</span>
                <strong>{formatQuantity(acc.totalLimit)} {acc.currency}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <span>{t('guarantees.currentBalance')}:</span>
                <strong style={{ color: 'var(--danger)' }}>{formatQuantity(acc.currentBalance)}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>{t('guarantees.available')}:</span>
                <strong style={{ color: 'var(--success)' }}>{formatQuantity(acc.availableLimit)}</strong>
              </div>
            </div>
            {acc.bankName && (
              <div style={{ marginTop: 10, fontSize: 12, color: 'var(--ink-500)' }}>
                {t('guarantees.bank')}: {acc.bankName}
              </div>
            )}
          </div>
        ))}
      </div>

      <h3 style={{ marginBottom: 12 }}>{t('guarantees.activeTitle')}</h3>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('common.date')}</th>
              <th>{t('guarantees.amount')}</th>
              <th>{t('guarantees.currency')}</th>
              <th>{t('reports.common.mrn')}</th>
              <th>{t('common.description')}</th>
              <th>{t('guarantees.expectedRelease')}</th>
            </tr>
          </thead>
          <tbody>
            {activeGuarantees.map((g, idx) => (
              <tr key={idx}>
                <td>{formatDate(g.entryDate)}</td>
                <td><strong>{formatQuantity(g.amount)}</strong></td>
                <td>{g.currency}</td>
                <td>{g.mrn || '-'}</td>
                <td>{g.description}</td>
                <td>{g.expectedReleaseDate ? formatDate(g.expectedReleaseDate) : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showAccountModal && (
        <div
          onClick={() => setShowAccountModal(false)}
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, backdropFilter: 'blur(2px)' }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{ background: 'white', borderRadius: 12, padding: 24, maxWidth: 640, width: '92%', boxShadow: 'var(--shadow-lg)' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
              <h3>{editingAccount ? t('guarantees.editAccount') : t('guarantees.newAccount')}</h3>
              <button onClick={() => setShowAccountModal(false)}>×</button>
            </div>
            <form onSubmit={handleSubmitAccount}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div className="form-group">
                  <label>{t('guarantees.accountNumber')} *</label>
                  <input type="text" value={accountFormData.accountNumber}
                    onChange={(e) => setAccountFormData({ ...accountFormData, accountNumber: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label>{t('guarantees.accountName')} *</label>
                  <input type="text" value={accountFormData.accountName}
                    onChange={(e) => setAccountFormData({ ...accountFormData, accountName: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label>{t('guarantees.bank')} *</label>
                  <select value={accountFormData.bankPartnerId}
                    onChange={(e) => setAccountFormData({ ...accountFormData, bankPartnerId: e.target.value })} required>
                    <option value="">{t('common.select')}...</option>
                    {partners.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>{t('guarantees.currency')} *</label>
                  <select value={accountFormData.currency}
                    onChange={(e) => setAccountFormData({ ...accountFormData, currency: e.target.value })}>
                    <option value="EUR">EUR</option><option value="USD">USD</option><option value="MKD">MKD</option>
                  </select>
                </div>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label>{t('guarantees.totalLimit')} *</label>
                  <input type="number" step="0.01" value={accountFormData.totalLimit}
                    onChange={(e) => setAccountFormData({ ...accountFormData, totalLimit: parseFloat(e.target.value) || 0 })} required />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
                <button type="button" onClick={() => setShowAccountModal(false)}>{t('common.cancel')}</button>
                <button type="submit" className="btn-primary">
                  {editingAccount ? t('common.save') : t('common.create')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showLedgerModal && (
        <div
          onClick={() => setShowLedgerModal(false)}
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, backdropFilter: 'blur(2px)' }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{ background: 'white', borderRadius: 12, padding: 24, maxWidth: 640, width: '92%', boxShadow: 'var(--shadow-lg)' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
              <h3>{t('guarantees.newLedgerEntry')}</h3>
              <button onClick={() => setShowLedgerModal(false)}>×</button>
            </div>
            <form onSubmit={handleSubmitLedger}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label>{t('guarantees.account')} *</label>
                  <select value={ledgerFormData.guaranteeAccountId}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, guaranteeAccountId: e.target.value })} required>
                    {accounts.map((acc) => (
                      <option key={acc.id} value={acc.id}>{acc.accountName} ({acc.accountNumber})</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>{t('guarantees.entryType')} *</label>
                  <select value={ledgerFormData.entryType}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, entryType: parseInt(e.target.value) })}>
                    <option value="0">{t('guarantees.debit')}</option>
                    <option value="1">{t('guarantees.credit')}</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>{t('guarantees.amount')} *</label>
                  <input type="number" step="0.01" value={ledgerFormData.amount}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, amount: parseFloat(e.target.value) || 0 })} required />
                </div>
                <div className="form-group">
                  <label>{t('guarantees.currency')} *</label>
                  <select value={ledgerFormData.currency}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, currency: e.target.value })}>
                    <option value="EUR">EUR</option><option value="USD">USD</option><option value="MKD">MKD</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>MRN</label>
                  <input type="text" value={ledgerFormData.mrn}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, mrn: e.target.value })} />
                </div>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label>{t('guarantees.expectedRelease')}</label>
                  <input type="date" value={ledgerFormData.expectedReleaseDate}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, expectedReleaseDate: e.target.value })} />
                </div>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label>{t('common.description')}</label>
                  <textarea value={ledgerFormData.description}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, description: e.target.value })} rows={3} />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
                <button type="button" onClick={() => setShowLedgerModal(false)}>{t('common.cancel')}</button>
                <button type="submit" className="btn-primary">{t('common.create')}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default Guarantees;
