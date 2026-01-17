import React from 'react';

interface SelectOption {
  value: string | number;
  label: string;
}

interface FormSelectSimpleProps {
  label: string;
  value: string | number;
  onChange: (value: any) => void;
  options: SelectOption[];
  required?: boolean;
  disabled?: boolean;
}

const FormSelectSimple: React.FC<FormSelectSimpleProps> = ({
  label,
  value,
  onChange,
  options,
  required = false,
  disabled = false,
}) => {
  return (
    <div className="form-group">
      <label>
        {label}
        {required && <span style={{ color: 'red' }}> *</span>}
      </label>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        required={required}
        disabled={disabled}
        className="form-control"
      >
        <option value="">-- Select {label} --</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
};

export default FormSelectSimple;
