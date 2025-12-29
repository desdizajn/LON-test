import React from 'react';
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Typography,
} from '@mui/material';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';

interface ErrorDisplayProps {
  error?: Error | string | null;
  message?: string;
  title?: string;
  onRetry?: () => void;
  fullPage?: boolean;
  actionButton?: React.ReactNode;
}

const ErrorDisplay: React.FC<ErrorDisplayProps> = ({
  error,
  message,
  title = 'Error',
  onRetry,
  fullPage = false,
  actionButton,
}) => {
  if (!error && !message) return null;

  const errorMessage = message || (typeof error === 'string' ? error : error?.message || 'Unknown error');

  if (fullPage) {
    return (
      <Box
        display="flex"
        flexDirection="column"
        alignItems="center"
        justifyContent="center"
        minHeight="400px"
        gap={3}
        p={3}
      >
        <ErrorOutlineIcon sx={{ fontSize: 60, color: 'error.main' }} />
        <Typography variant="h5" color="error">
          {title}
        </Typography>
        <Typography variant="body1" color="text.secondary" align="center" maxWidth="600px">
          {errorMessage}
        </Typography>
        {onRetry && (
          <Button variant="contained" color="primary" onClick={onRetry}>
            Try Again
          </Button>
        )}
        {actionButton && actionButton}
      </Box>
    );
  }

  return (
    <Alert
      severity="error"
      action={
        <>
          {onRetry && (
            <Button color="inherit" size="small" onClick={onRetry}>
              Retry
            </Button>
          )}
          {actionButton && actionButton}
        </>
      }
    >
      <AlertTitle>{title}</AlertTitle>
      {errorMessage}
    </Alert>
  );
};

export default ErrorDisplay;
