import React from 'react';
import { Autocomplete, TextField } from '@mui/material';
import { Controller, Control, FieldValues, Path } from 'react-hook-form';

interface AutocompleteOption {
  id: string;
  label: string;
}

interface FormAutocompleteProps<T extends FieldValues> {
  name: Path<T>;
  control: Control<T>;
  label: string;
  options: AutocompleteOption[];
  defaultValue?: string;
  rules?: any;
  disabled?: boolean;
  loading?: boolean;
}

function FormAutocomplete<T extends FieldValues>({
  name,
  control,
  label,
  options,
  defaultValue = '',
  rules,
  disabled = false,
  loading = false,
}: FormAutocompleteProps<T>) {
  return (
    <Controller
      name={name}
      control={control}
      defaultValue={defaultValue as any}
      rules={rules}
      render={({ field: { onChange, value }, fieldState: { error } }) => (
        <Autocomplete
          options={options}
          getOptionLabel={(option) => 
            typeof option === 'string' ? option : option.label
          }
          value={options.find((opt) => opt.id === value) || null}
          onChange={(_, data) => onChange(data?.id || '')}
          disabled={disabled}
          loading={loading}
          renderInput={(params) => (
            <TextField
              {...params}
              label={label}
              error={!!error}
              helperText={error?.message}
              margin="normal"
              fullWidth
            />
          )}
        />
      )}
    />
  );
}

export default FormAutocomplete;
