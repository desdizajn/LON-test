import React from 'react';
import {
  FormControl,
  FormControlLabel,
  Checkbox,
  FormHelperText,
} from '@mui/material';
import { Controller, Control, FieldValues, Path } from 'react-hook-form';

interface FormCheckboxProps<T extends FieldValues> {
  name: Path<T>;
  control: Control<T>;
  label: string;
  defaultValue?: boolean;
  disabled?: boolean;
}

function FormCheckbox<T extends FieldValues>({
  name,
  control,
  label,
  defaultValue = false,
  disabled = false,
}: FormCheckboxProps<T>) {
  return (
    <Controller
      name={name}
      control={control}
      defaultValue={defaultValue as any}
      render={({ field, fieldState: { error } }) => (
        <FormControl error={!!error} margin="normal">
          <FormControlLabel
            control={
              <Checkbox
                {...field}
                checked={field.value}
                disabled={disabled}
              />
            }
            label={label}
          />
          {error && <FormHelperText>{error.message}</FormHelperText>}
        </FormControl>
      )}
    />
  );
}

export default FormCheckbox;
