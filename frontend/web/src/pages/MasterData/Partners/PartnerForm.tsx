import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Box, Grid } from '@mui/material';
import FormDialog from '../../../components/common/FormDialog';
import FormInput from '../../../components/forms/FormInput';
import FormSelect from '../../../components/forms/FormSelect';
import FormCheckbox from '../../../components/forms/FormCheckbox';
import { partnersApi } from '../../../services/masterDataApi';
import { useMasterDataStore } from '../../../store/useMasterDataStore';
import { showSuccess, showError } from '../../../utils/toast';
import type { Partner, PartnerFormData } from '../../../types/masterData';

interface PartnerFormProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  partner?: Partner | null;
}

const PartnerForm: React.FC<PartnerFormProps> = ({ open, onClose, onSuccess, partner }) => {
  const { addPartner, updatePartner } = useMasterDataStore();
  const [submitting, setSubmitting] = useState(false);

  const { control, handleSubmit, reset } = useForm<PartnerFormData>({
    defaultValues: {
      code: partner?.code || '',
      name: partner?.name || '',
      partnerType: partner?.partnerType || 1,
      taxNumber: partner?.taxNumber || '',
      vatNumber: partner?.vatNumber || '',
      eoriNumber: partner?.eoriNumber || '',
      address: partner?.address || '',
      city: partner?.city || '',
      postalCode: partner?.postalCode || '',
      country: partner?.country || '',
      contactPerson: partner?.contactPerson || '',
      phone: partner?.phone || '',
      email: partner?.email || '',
      isActive: partner?.isActive !== false,
    },
  });

  useEffect(() => {
    if (partner) {
      reset({
        code: partner.code,
        name: partner.name,
        partnerType: partner.partnerType,
        taxNumber: partner.taxNumber || '',
        vatNumber: partner.vatNumber || '',
        eoriNumber: partner.eoriNumber || '',
        address: partner.address || '',
        city: partner.city || '',
        postalCode: partner.postalCode || '',
        country: partner.country || '',
        contactPerson: partner.contactPerson || '',
        phone: partner.phone || '',
        email: partner.email || '',
        isActive: partner.isActive,
      });
    }
  }, [partner, reset]);

  const onSubmit = async (data: PartnerFormData) => {
    try {
      setSubmitting(true);
      if (partner) {
        const response = await partnersApi.update(partner.id, data);
        updatePartner(partner.id, response.data);
        showSuccess('Partner updated successfully');
      } else {
        const response = await partnersApi.create(data);
        addPartner(response.data);
        showSuccess('Partner created successfully');
      }
      onSuccess();
    } catch (err: any) {
      showError(err.response?.data?.message || 'Failed to save partner');
    } finally {
      setSubmitting(false);
    }
  };

  const partnerTypeOptions = [
    { value: 1, label: 'Supplier' },
    { value: 2, label: 'Customer' },
    { value: 3, label: 'Carrier' },
    { value: 4, label: 'Bank' },
  ];

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={partner ? 'Edit Partner' : 'New Partner'}
      onSubmit={handleSubmit(onSubmit)}
      isSubmitting={submitting}
      maxWidth="md"
    >
      <Box>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="code"
              control={control}
              label="Partner Code"
              rules={{ required: 'Partner code is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="name"
              control={control}
              label="Partner Name"
              rules={{ required: 'Partner name is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormSelect
              name="partnerType"
              control={control}
              label="Partner Type"
              options={partnerTypeOptions}
              rules={{ required: 'Partner type is required' }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="taxNumber" control={control} label="Tax Number" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="vatNumber" control={control} label="VAT Number" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="eoriNumber" control={control} label="EORI Number" />
          </Grid>
          <Grid item xs={12}>
            <FormInput name="address" control={control} label="Address" multiline rows={2} />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormInput name="city" control={control} label="City" />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormInput name="postalCode" control={control} label="Postal Code" />
          </Grid>
          <Grid item xs={12} sm={4}>
            <FormInput name="country" control={control} label="Country" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="contactPerson" control={control} label="Contact Person" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput name="phone" control={control} label="Phone" />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormInput
              name="email"
              control={control}
              label="Email"
              rules={{
                pattern: {
                  value: /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
                  message: 'Invalid email address',
                },
              }}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <FormCheckbox name="isActive" control={control} label="Active" />
          </Grid>
        </Grid>
      </Box>
    </FormDialog>
  );
};

export default PartnerForm;
