import React from 'react';
import { Navigate } from 'react-router-dom';

/**
 * P10.7 — HR payroll export.
 *
 * The aggregated payroll view lives under /finance/payroll (shared backend +
 * CSV export). HR group links here; Finance group links to the same page.
 */
const PayrollExport: React.FC = () => <Navigate to="/finance/payroll" replace />;

export default PayrollExport;
