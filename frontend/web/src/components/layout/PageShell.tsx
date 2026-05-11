import React from 'react';
import { Box, Breadcrumbs, Link as MuiLink, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

export interface PageBreadcrumb {
  label: string;
  to?: string;
}

interface PageShellProps {
  title: React.ReactNode;
  /** Right-aligned action slot (typically buttons). */
  actions?: React.ReactNode;
  breadcrumbs?: PageBreadcrumb[];
  /** Optional subtitle shown under the title. */
  subtitle?: React.ReactNode;
  children: React.ReactNode;
}

/**
 * P16.B3 — Consistent page chrome for migrated pages.
 *
 * - Responsive max width (lg).
 * - Sticky header height + spacing rules so 91 different layouts
 *   collapse into one predictable shell.
 * - Action slot is right-aligned and wraps on narrow viewports.
 * - Breadcrumbs render as MUI `<Breadcrumbs>` when supplied; the last
 *   entry is plain text (no link).
 */
const PageShell: React.FC<PageShellProps> = ({
  title,
  actions,
  breadcrumbs,
  subtitle,
  children,
}) => {
  return (
    <Box
      sx={{
        width: '100%',
        maxWidth: 1440,
        mx: 'auto',
        px: { xs: 1.5, sm: 2, md: 3 },
        py: { xs: 2, md: 3 },
      }}
    >
      {breadcrumbs && breadcrumbs.length > 0 && (
        <Breadcrumbs aria-label="breadcrumb" sx={{ mb: 1 }}>
          {breadcrumbs.map((b, i) =>
            b.to && i < breadcrumbs.length - 1 ? (
              <MuiLink
                key={i}
                component={RouterLink}
                to={b.to}
                color="inherit"
                underline="hover"
              >
                {b.label}
              </MuiLink>
            ) : (
              <Typography key={i} color="text.primary">
                {b.label}
              </Typography>
            )
          )}
        </Breadcrumbs>
      )}

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        alignItems={{ xs: 'flex-start', md: 'center' }}
        justifyContent="space-between"
        spacing={1.5}
        sx={{ mb: 2 }}
      >
        <Box>
          <Typography variant="h1" component="h1" sx={{ fontSize: { xs: '1.5rem', md: '1.875rem' } }}>
            {title}
          </Typography>
          {subtitle && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {subtitle}
            </Typography>
          )}
        </Box>
        {actions && (
          <Stack
            direction="row"
            spacing={1}
            flexWrap="wrap"
            useFlexGap
            sx={{ width: { xs: '100%', md: 'auto' }, justifyContent: { xs: 'flex-start', md: 'flex-end' } }}
          >
            {actions}
          </Stack>
        )}
      </Stack>

      <Box>{children}</Box>
    </Box>
  );
};

export default PageShell;
