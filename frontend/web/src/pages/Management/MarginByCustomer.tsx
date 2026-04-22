import React from 'react';
import { Navigate } from 'react-router-dom';

/**
 * P13.4 — Margin by customer.
 *
 * Shares the same view as /finance/margin (revenue per customer, outstanding,
 * orders, produced qty). This route is the Management-group entry point so
 * KPI viewers can land here directly.
 */
const MarginByCustomer: React.FC = () => {
  return <Navigate to="/finance/margin" replace />;
};

export default MarginByCustomer;
