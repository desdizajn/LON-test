import React from 'react';

interface FormInputSimpleProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'number' | 'date' | 'email';
  required?: boolean;
  disabled?: boolean;
  multiline?: boolean;
  min?: number;
  max?: number;
  placeholder?: string;
}

const FormInputSimple: React.FC<FormInputSimpleProps> = ({
  label,
  value,
  onChange,
  type = 'text',
  required = false,
  disabled = false,
  multiline = false,
  min,
  max,
  placeholder,
}) => {
  return (
    <div className="form-group">
      <label>
        {label}
        {required && <span style={{ color: 'red' }}> *</span>}
      </label>
      {multiline ? (
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          required={required}
          disabled={disabled}
          placeholder={placeholder}
          className="form-control"
          rows={4}
        />
      ) : (
        <input
          type={type}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          required={required}
          disabled={disabled}
          min={min}
          max={max}
          placeholder={placeholder}
          className="form-control"
        />
      )}
    </div>
  );
};

export default FormInputSimple;
