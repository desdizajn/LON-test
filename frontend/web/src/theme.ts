import { createTheme } from '@mui/material/styles';

/**
 * P16.B3 — MUI theme honoured by the global ThemeProvider in App.tsx.
 *
 * Palette mirrors the existing `--taris-*` CSS variables in index.css
 * so MUI components blend with the legacy CSS-variable-driven pages
 * during the gradual migration. When pages are fully on MUI, the CSS
 * vars can be retired.
 *
 *   primary   = taris-blue-500   #1e88e5
 *   secondary = taris-red-500    #e53935  (logo accent)
 *   success   = taris-green-500  #2e7d32  (taken from existing --success)
 *   warning   = taris-amber-500  #f59e0b
 *   error     = taris-red-600    #c62828
 */
const theme = createTheme({
  palette: {
    primary: {
      light: '#5aa7e0',
      main: '#1e88e5',
      dark: '#1565c0',
      contrastText: '#ffffff',
    },
    secondary: {
      light: '#ef5350',
      main: '#e53935',
      dark: '#c62828',
      contrastText: '#ffffff',
    },
    success: { main: '#2e7d32' },
    warning: { main: '#f59e0b' },
    error: { main: '#c62828' },
    info: { main: '#1e88e5' },
    background: {
      default: '#f8fafc',
      paper: '#ffffff',
    },
  },
  typography: {
    fontFamily:
      'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
    h1: { fontSize: '1.875rem', fontWeight: 600 },
    h2: { fontSize: '1.5rem', fontWeight: 600 },
    h3: { fontSize: '1.25rem', fontWeight: 600 },
  },
  shape: {
    borderRadius: 6,
  },
  components: {
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: { root: { textTransform: 'none' } },
    },
    MuiAlert: {
      styleOverrides: { root: { borderRadius: 6 } },
    },
  },
});

export default theme;
