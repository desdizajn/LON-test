import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import BulkFieldUpdateButton from './BulkFieldUpdateButton';

// react-i18next stub: returns the key (and optional interpolation values) so
// the test asserts against keys instead of translated strings.
jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string, opts?: Record<string, unknown>) =>
      opts ? `${key}|${JSON.stringify(opts)}` : key,
  }),
}));

describe('BulkFieldUpdateButton', () => {
  it('renders the button with label', () => {
    render(
      <BulkFieldUpdateButton
        fieldName="currency"
        label="Change currency on all lines"
        onConfirm={() => undefined}
      />,
    );
    expect(screen.getByTestId('bulk-update-currency')).toBeInTheDocument();
    expect(screen.getByText('Change currency on all lines')).toBeInTheDocument();
  });

  it('respects disabled prop', () => {
    render(
      <BulkFieldUpdateButton
        fieldName="currency"
        label="x"
        onConfirm={() => undefined}
        disabled
      />,
    );
    expect(screen.getByTestId('bulk-update-currency')).toBeDisabled();
  });

  it('opens confirm dialog on click', () => {
    render(
      <BulkFieldUpdateButton
        fieldName="uom"
        label="Apply UoM"
        onConfirm={() => undefined}
      />,
    );
    fireEvent.click(screen.getByTestId('bulk-update-uom'));
    // ConfirmDialog renders the title text from the t() key.
    expect(
      screen.getByText(/common\.bulkUpdate\.title/),
    ).toBeInTheDocument();
  });

  it('shows recalcWarning text in dialog when provided', () => {
    render(
      <BulkFieldUpdateButton
        fieldName="currency"
        label="Change"
        onConfirm={() => undefined}
        recalcWarning="Recalculation needed."
      />,
    );
    fireEvent.click(screen.getByTestId('bulk-update-currency'));
    expect(screen.getByText(/Recalculation needed\./)).toBeInTheDocument();
  });

  it('invokes onConfirm with a reason marker when user confirms', async () => {
    const onConfirm = jest.fn();
    render(
      <BulkFieldUpdateButton
        fieldName="tariffCode"
        label="Apply tariff"
        onConfirm={onConfirm}
      />,
    );
    fireEvent.click(screen.getByTestId('bulk-update-tariffCode'));
    fireEvent.click(screen.getByText('common.apply'));
    await waitFor(() => expect(onConfirm).toHaveBeenCalledTimes(1));
    expect(onConfirm).toHaveBeenCalledWith('bulk-update');
  });

  it('does not invoke onConfirm when user cancels', () => {
    const onConfirm = jest.fn();
    render(
      <BulkFieldUpdateButton
        fieldName="country"
        label="Apply country"
        onConfirm={onConfirm}
      />,
    );
    fireEvent.click(screen.getByTestId('bulk-update-country'));
    fireEvent.click(screen.getByText('common.cancel'));
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
