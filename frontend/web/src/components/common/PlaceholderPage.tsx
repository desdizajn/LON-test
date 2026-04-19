import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

export type BackendStatus = 'missing' | 'partial' | 'exists';

export interface PlaceholderPageProps {
  /** i18n key для групата (e.g. "nav.warehouse") — shown in breadcrumb */
  groupKey: string;
  /** i18n key for this item title (e.g. "nav.warehouse.incoming") */
  titleKey: string;
  /** Work plan reference (e.g. "P2.3.4") — anchor link to WORK_PLAN.md */
  workPlanRef: string;
  /** Backend readiness for this view */
  backendStatus: BackendStatus;
  /**
   * Pre-translated hint describing what existing data/page this relates to.
   * Optional. Shown if provided. Not i18n-keyed to keep placeholder wiring cheap —
   * когa view-от ќe добиe реален page, и copy-то се локализира тогаш.
   */
  existingDataHint?: string;
  /**
   * Pre-translated sentence explaining what this view will do when built.
   * Optional. Recommended for UX clarity.
   */
  plannedBehavior?: string;
}

const statusStyle = (status: BackendStatus): React.CSSProperties => {
  const base: React.CSSProperties = {
    display: 'inline-block',
    padding: '4px 10px',
    borderRadius: '12px',
    fontSize: '12px',
    fontWeight: 600,
  };
  switch (status) {
    case 'missing':
      return { ...base, background: '#fed7d7', color: '#c53030' };
    case 'partial':
      return { ...base, background: '#feebc8', color: '#c05621' };
    case 'exists':
      return { ...base, background: '#c6f6d5', color: '#276749' };
  }
};

const PlaceholderPage: React.FC<PlaceholderPageProps> = ({
  groupKey,
  titleKey,
  workPlanRef,
  backendStatus,
  existingDataHint,
  plannedBehavior,
}) => {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div style={{ padding: '24px', maxWidth: '900px', margin: '0 auto' }}>
      {/* Breadcrumb */}
      <div
        style={{
          fontSize: '13px',
          color: '#718096',
          marginBottom: '12px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
        }}
      >
        <span>{t(groupKey)}</span>
        <span>›</span>
        <span style={{ color: '#2d3748', fontWeight: 500 }}>{t(titleKey)}</span>
      </div>

      {/* Card */}
      <div
        style={{
          background: 'white',
          border: '1px solid #e2e8f0',
          borderRadius: '12px',
          padding: '32px',
          boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
        }}
      >
        {/* Title + status pill */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
            marginBottom: '24px',
            flexWrap: 'wrap',
          }}
        >
          <h1 style={{ margin: 0, fontSize: '28px', color: '#1a202c' }}>
            {t(titleKey)}
          </h1>
          <span style={statusStyle(backendStatus)}>
            {t(`placeholder.status.${backendStatus}`)}
          </span>
        </div>

        {/* Big "Coming soon" banner */}
        <div
          style={{
            background: '#fff5f5',
            border: '1px dashed #fc8181',
            borderRadius: '8px',
            padding: '20px',
            marginBottom: '24px',
            display: 'flex',
            alignItems: 'center',
            gap: '16px',
          }}
        >
          <span style={{ fontSize: '32px' }}>🚧</span>
          <div>
            <div style={{ fontWeight: 600, fontSize: '16px', color: '#c53030' }}>
              {t('placeholder.comingSoon')}
            </div>
            <div style={{ fontSize: '14px', color: '#742a2a', marginTop: '4px' }}>
              {t('placeholder.comingSoonSubtitle')}
            </div>
          </div>
        </div>

        {/* Details table */}
        <div style={{ display: 'grid', gap: '16px', marginBottom: '24px' }}>
          {plannedBehavior && (
            <div>
              <div
                style={{
                  fontSize: '12px',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                  color: '#4a5568',
                  marginBottom: '4px',
                  letterSpacing: '0.5px',
                }}
              >
                {t('placeholder.plannedBehavior')}
              </div>
              <div style={{ fontSize: '14px', color: '#2d3748', lineHeight: 1.6 }}>
                {plannedBehavior}
              </div>
            </div>
          )}

          {existingDataHint && (
            <div>
              <div
                style={{
                  fontSize: '12px',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                  color: '#4a5568',
                  marginBottom: '4px',
                  letterSpacing: '0.5px',
                }}
              >
                {t('placeholder.existingData')}
              </div>
              <div style={{ fontSize: '14px', color: '#2d3748', lineHeight: 1.6 }}>
                {existingDataHint}
              </div>
            </div>
          )}

          <div>
            <div
              style={{
                fontSize: '12px',
                textTransform: 'uppercase',
                fontWeight: 600,
                color: '#4a5568',
                marginBottom: '4px',
                letterSpacing: '0.5px',
              }}
            >
              {t('placeholder.workPlanRef')}
            </div>
            <div style={{ fontSize: '14px', color: '#2d3748' }}>
              <code
                style={{
                  background: '#edf2f7',
                  padding: '2px 8px',
                  borderRadius: '4px',
                  fontSize: '13px',
                }}
              >
                {workPlanRef}
              </code>
            </div>
          </div>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
          <button
            onClick={() => navigate('/dashboard')}
            style={{
              padding: '10px 20px',
              background: '#4299e1',
              color: 'white',
              border: 'none',
              borderRadius: '6px',
              fontSize: '14px',
              fontWeight: 500,
              cursor: 'pointer',
            }}
          >
            {t('placeholder.backToDashboard')}
          </button>
          <button
            onClick={() => navigate(-1)}
            style={{
              padding: '10px 20px',
              background: 'white',
              color: '#4a5568',
              border: '1px solid #cbd5e0',
              borderRadius: '6px',
              fontSize: '14px',
              fontWeight: 500,
              cursor: 'pointer',
            }}
          >
            {t('common.back')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default PlaceholderPage;
