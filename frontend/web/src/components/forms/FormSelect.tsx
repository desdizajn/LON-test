import React from 'react';
import {
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
} from '@mui/material';
import { Controller, Control, FieldValues, Path } from 'react-hook-form';

interface SelectOption {
  value: string | number;
  label: string;
}

interface FormSelectProps<T extends FieldValues> {
  name: Path<T>;
  control: Control<T>;
  label: string;
  options: SelectOption[];
  defaultValue?: string | number;
  rules?: any;
  disabled?: boolean;
}

function FormSelect<T extends FieldValues>({
  name,
  control,
  label,
  options,
  defaultValue = '',
  rules,
  disabled = false,
}: FormSelectProps<T>) {
  return (
    <Controller
      name={name}
      control={control}
      defaultValue={defaultValue as any}
      rules={rules}
      render={({ field, fieldState: { error } }) => (
        <FormControl fullWidth margin="normal" error={!!error} disabled={disabled}>
          <InputLabel>{label}</InputLabel>
          <Select {...field} label={label}>
            {options.map((option) => (
              <MenuItem key={option.value} value={option.value}>
                {option.label}
              </MenuItem>
            ))}
          </Select>
          {error && <FormHelperText>{error.message}</FormHelperText>}
        </FormControl>
      )}
    />
  );
}

export default FormSelect;
