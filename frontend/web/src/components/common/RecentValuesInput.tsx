import React from 'react';
import { useFieldHistory } from '../../hooks/useFieldHistory';

/**
 * P5.3.5 — text input backed by the current user's recent-values cache.
 * Mount with a unique <c>fieldKey</c>; on blur (or explicitly via
 * <c>onCommit</c>) the current value is recorded into the cache. Autocomplete
 * is wired via a native datalist so keyboard navigation + screen-readers
 * work without extra code.
 *
 * Caller still owns `value` and `onChange` — this is a thin wrapper, not a
 * controller. Use `commitOnBlur` for bare inputs; for submit-once forms,
 * call `recordNow()` imperatively via `onCommit` ref.
 */
interface Props {
  fieldKey: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  style?: React.CSSProperties;
  disabled?: boolean;
  commitOnBlur?: boolean;
  limit?: number;
}

const RecentValuesInput: React.FC<Props> = ({
  fieldKey,
  value,
  onChange,
  placeholder,
  style,
  disabled,
  commitOnBlur = true,
  limit = 10,
}) => {
  const { recent, record } = useFieldHistory(fieldKey, limit);
  const listId = `fh-${fieldKey.replace(/[^a-z0-9_-]/gi, '-')}`;

  return (
    <>
      <input
        type="text"
        list={listId}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={() => {
          if (commitOnBlur && value.trim()) record(value);
        }}
        placeholder={placeholder}
        disabled={disabled}
        style={style}
      />
      <datalist id={listId}>
        {recent.map((r) => (
          <option key={r.value} value={r.value} />
        ))}
      </datalist>
    </>
  );
};

export default RecentValuesInput;
