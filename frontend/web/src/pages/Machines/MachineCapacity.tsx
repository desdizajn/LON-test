import React from 'react';
import { Navigate } from 'react-router-dom';

/**
 * P11.4a — Machines → Capacity.
 *
 * Reuses /management/capacity (same aggregation). The Machines group links
 * here; Management group links to the same view.
 */
const MachineCapacity: React.FC = () => <Navigate to="/management/capacity" replace />;

export default MachineCapacity;
