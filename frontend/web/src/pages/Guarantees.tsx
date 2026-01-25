import React, { useEffect, useState } from 'react';
import { guaranteeApi, masterDataApi } from '../services/api';

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
  const [accounts, setAccounts] = useState<GuaranteeAccount[]>([]);
  const [activeGuarantees, setActiveGuarantees] = useState<any[]>([]);
  const [partners, setPartners] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAccountModal, setShowAccountModal] = useState(false);
  const [showLedgerModal, setShowLedgerModal] = useState(false);
  const [editingAccount, setEditingAccount] = useState<GuaranteeAccount | null>(null);

  const [accountFormData, setAccountFormData] = useState({
    accountNumber: '',
    accountName: '',
    bankPartnerId: '',
    currency: 'EUR',
    totalLimit: 0,
    isActive: true
  });

  const [ledgerFormData, setLedgerFormData] = useState({
    guaranteeAccountId: '',
    entryType: 0, // 0 = Debit, 1 = Credit
    amount: 0,
    currency: 'EUR',
    mrn: '',
    description: '',
    expectedReleaseDate: ''
  });

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [accountsRes, guaranteesRes, partnersRes] = await Promise.all([
        guaranteeApi.getAccounts(),
        guaranteeApi.getActiveGuarantees(),
        masterDataApi.getPartners()
      ]);
      setAccounts(accountsRes.data);
      setActiveGuarantees(guaranteesRes.data);
      setPartners(partnersRes.data.filter((p: any) => p.partnerType === 2)); // Bank partners
    } catch (err) {
      console.error('Failed to load guarantees', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateAccount = () => {
    setEditingAccount(null);
    setAccountFormData({
      accountNumber: '',
      accountName: '',
      bankPartnerId: '',
      currency: 'EUR',
      totalLimit: 0,
      isActive: true
    });
    setShowAccountModal(true);
  };

  const handleEditAccount = (account: GuaranteeAccount) => {
    setEditingAccount(account);
    setAccountFormData({
      accountNumber: account.accountNumber,
      accountName: account.accountName,
      bankPartnerId: '', // You'd need to store this
      currency: account.currency,
      totalLimit: account.totalLimit,
      isActive: true
    });
    setShowAccountModal(true);
  };

  const handleSubmitAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingAccount) {
        await guaranteeApi.updateAccount(editingAccount.id, accountFormData);
      } else {
        await guaranteeApi.createAccount(accountFormData);
      }
      setShowAccountModal(false);
      loadData();
    } catch (err) {
      console.error('Failed to save account', err);
      alert('Failed to save account');
    }
  };

  const handleCreateLedgerEntry = () => {
    setLedgerFormData({
      guaranteeAccountId: accounts.length > 0 ? accounts[0].id : '',
      entryType: 0,
      amount: 0,
      currency: 'EUR',
      mrn: '',
      description: '',
      expectedReleaseDate: ''
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
      alert(err.response?.data?.message || 'Failed to create ledger entry');
    }
  };

  if (loading) return <div className="loading">Loading guarantees...</div>;

  return (
    <div>
      <div className="header">
        <h2>Guarantee Management</h2>
        <div>
          <button onClick={handleCreateAccount} className="primary-button" style={{ marginRight: '10px' }}>
            + Нова гаранциона сметка
          </button>
          <button onClick={handleCreateLedgerEntry} className="secondary-button">
            + Нова трансакција
          </button>
        </div>
      </div>

      <h3 style={{ marginBottom: '15px' }}>Guarantee Accounts</h3>
      <div className="card-grid" style={{ marginBottom: '30px' }}>
        {accounts.map((acc) => (
          <div key={acc.id} className="card" onClick={() => handleEditAccount(acc)} style={{ cursor: 'pointer' }}>
            <h3>{acc.accountName}</h3>
            <div style={{ fontSize: '12px', color: '#888', marginBottom: '5px' }}>{acc.accountNumber}</div>
            <div style={{ fontSize: '14px', marginTop: '10px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                <span>Total Limit:</span>
                <strong>{acc.totalLimit.toFixed(2)} {acc.currency}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '5px' }}>
                <span>Current Balance:</span>
                <strong style={{ color: '#e74c3c' }}>{acc.currentBalance.toFixed(2)}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Available:</span>
                <strong style={{ color: '#27ae60' }}>{acc.availableLimit.toFixed(2)}</strong>
              </div>
            </div>
            {acc.bankName && (
              <div style={{ marginTop: '10px', fontSize: '12px', color: '#666' }}>
                Банка: {acc.bankName}
              </div>
            )}
          </div>
        ))}
      </div>

      <h3 style={{ marginBottom: '15px' }}>Active Guarantees</h3>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Amount</th>
              <th>Currency</th>
              <th>MRN</th>
              <th>Description</th>
              <th>Expected Release</th>
            </tr>
          </thead>
          <tbody>
            {activeGuarantees.map((g, idx) => (
              <tr key={idx}>
                <td>{new Date(g.entryDate).toLocaleDateString()}</td>
                <td><strong>{g.amount.toFixed(2)}</strong></td>
                <td>{g.currency}</td>
                <td>{g.mrn || '-'}</td>
                <td>{g.description}</td>
                <td>{g.expectedReleaseDate ? new Date(g.expectedReleaseDate).toLocaleDateString() : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Account Modal */}
      {showAccountModal && (
        <div className="modal-overlay" onClick={() => setShowAccountModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{editingAccount ? 'Измени гаранциона сметка' : 'Нова гаранциона сметка'}</h3>
              <button onClick={() => setShowAccountModal(false)} className="close-button">×</button>
            </div>
            <form onSubmit={handleSubmitAccount}>
              <div className="form-grid">
                <div className="form-group">
                  <label>Број на сметка *</label>
                  <input
                    type="text"
                    value={accountFormData.accountNumber}
                    onChange={(e) => setAccountFormData({ ...accountFormData, accountNumber: e.target.value })}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Име на сметка *</label>
                  <input
                    type="text"
                    value={accountFormData.accountName}
                    onChange={(e) => setAccountFormData({ ...accountFormData, accountName: e.target.value })}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Банка *</label>
                  <select
                    value={accountFormData.bankPartnerId}
                    onChange={(e) => setAccountFormData({ ...accountFormData, bankPartnerId: e.target.value })}
                    required
                  >
                    <option value="">Избери...</option>
                    {partners.map((p) => (
                      <option key={p.id} value={p.id}>{p.name}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Валута *</label>
                  <select
                    value={accountFormData.currency}
                    onChange={(e) => setAccountFormData({ ...accountFormData, currency: e.target.value })}
                  >
                    <option value="EUR">EUR</option>
                    <option value="USD">USD</option>
                    <option value="MKD">MKD</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Вкупен лимит *</label>
                  <input
                    type="number"
                    step="0.01"
                    value={accountFormData.totalLimit}
                    onChange={(e) => setAccountFormData({ ...accountFormData, totalLimit: parseFloat(e.target.value) || 0 })}
                    required
                  />
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" onClick={() => setShowAccountModal(false)} className="secondary-button">
                  Откажи
                </button>
                <button type="submit" className="primary-button">
                  {editingAccount ? 'Зачувај' : 'Креирај'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Ledger Entry Modal */}
      {showLedgerModal && (
        <div className="modal-overlay" onClick={() => setShowLedgerModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Нова трансакција</h3>
              <button onClick={() => setShowLedgerModal(false)} className="close-button">×</button>
            </div>
            <form onSubmit={handleSubmitLedger}>
              <div className="form-grid">
                <div className="form-group">
                  <label>Гаранциона сметка *</label>
                  <select
                    value={ledgerFormData.guaranteeAccountId}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, guaranteeAccountId: e.target.value })}
                    required
                  >
                    {accounts.map((acc) => (
                      <option key={acc.id} value={acc.id}>
                        {acc.accountName} ({acc.accountNumber})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Тип *</label>
                  <select
                    value={ledgerFormData.entryType}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, entryType: parseInt(e.target.value) })}
                  >
                    <option value="0">Задолжување (Debit)</option>
                    <option value="1">Одобрување (Credit)</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Износ *</label>
                  <input
                    type="number"
                    step="0.01"
                    value={ledgerFormData.amount}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, amount: parseFloat(e.target.value) || 0 })}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Валута *</label>
                  <select
                    value={ledgerFormData.currency}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, currency: e.target.value })}
                  >
                    <option value="EUR">EUR</option>
                    <option value="USD">USD</option>
                    <option value="MKD">MKD</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>MRN</label>
                  <input
                    type="text"
                    value={ledgerFormData.mrn}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, mrn: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label>Очекуван датум на ослободување</label>
                  <input
                    type="date"
                    value={ledgerFormData.expectedReleaseDate}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, expectedReleaseDate: e.target.value })}
                  />
                </div>
                <div className="form-group full-width">
                  <label>Опис</label>
                  <textarea
                    value={ledgerFormData.description}
                    onChange={(e) => setLedgerFormData({ ...ledgerFormData, description: e.target.value })}
                    rows={3}
                  />
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" onClick={() => setShowLedgerModal(false)} className="secondary-button">
                  Откажи
                </button>
                <button type="submit" className="primary-button">
                  Креирај
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default Guarantees;
