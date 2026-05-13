import React, { useState } from 'react';
import { Button, Tooltip } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import { useTranslation } from 'react-i18next';
import ConfirmDialog from './ConfirmDialog';

/**
 * Bulk field-update button + confirm dialog.
 *
 * Per BLUEPRINT §7.3.1 + AGENT-PROMPTS §E0 (rescoped 2026-05-12):
 * generic toolbar action that lets the user change one field across all lines
 * of a document in one click. Primary use cases (where TEKSPORT data has
 * variance):
 *   - UoM
 *   - CountryOfOrigin
 *   - TariffCode
 *   - Currency (rare; TEKSPORT is 99.998% EUR)
 *
 * The component is presentational: it owns the confirmation UX. Caller wires
 * `onConfirm` to its API mutation (which must include audit `reason`).
 *
 * Example:
 *   <BulkFieldUpdateButton
 *     fieldName="currency"
 *     label={t('common.bulkUpdate.currency.label')}
 *     onConfirm={async (reason) => mutate({ field: 'Currency', value: 'EUR', reason })}
 *     recalcWarning={t('common.bulkUpdate.currency.recalcWarning')}
 *     disabled={!hasMultipleLines}
 *   />
 */
export interface BulkFieldUpdateButtonProps {
  /** The field being updated (informational; not sent to server by this component). */
  fieldName: string;
  /** Button label shown to user. */
  label: string;
  /** Called when user confirms. Receives a free-text reason from the user. */
  onConfirm: (reason: string) => void | Promise<void>;
  /** Optional warning to display in the dialog (e.g. "this will recalculate values"). */
  recalcWarning?: string;
  /** Disable the button (e.g. when only 1 line; nothing to bulk-update). */
  disabled?: boolean;
}

const BulkFieldUpdateButton: React.FC<BulkFieldUpdateButtonProps> = ({
  fieldName,
  label,
  onConfirm,
  recalcWarning,
  disabled = false,
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  const dialogMessage = recalcWarning
    ? `${t('common.bulkUpdate.confirm', { field: fieldName })}\n\n${recalcWarning}`
    : t('common.bulkUpdate.confirm', { field: fieldName });

  return (
    <>
      <Tooltip title={t('common.stickyDefaults.tooltip')}>
        <span>
          <Button
            variant="outlined"
            size="small"
            startIcon={<EditIcon />}
            onClick={() => setOpen(true)}
            disabled={disabled}
            data-testid={`bulk-update-${fieldName}`}
          >
            {label}
          </Button>
        </span>
      </Tooltip>
      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        onConfirm={async () => {
          // For v1 we use a fixed reason marker; future iterations can add an
          // optional reason textarea inside the dialog. Audit log captures
          // `Actor` + `OccurredAt` regardless.
          await onConfirm('bulk-update');
          setOpen(false);
        }}
        title={t('common.bulkUpdate.title', { field: fieldName })}
        message={dialogMessage}
        confirmText={t('common.apply')}
        cancelText={t('common.cancel')}
        confirmColor="warning"
      />
    </>
  );
};

export default BulkFieldUpdateButton;
