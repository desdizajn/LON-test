import React from 'react';
import { Navigate } from 'react-router-dom';

/**
 * P9.4 — Packing view.
 *
 * Routes to the same view as /finished/awaiting-pack (finished items ready
 * to be packed + remaining qty). Active workstations see the same queue.
 */
const Packing: React.FC = () => <Navigate to="/finished/awaiting-pack" replace />;

export default Packing;
