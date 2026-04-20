import { useMemo } from 'react';
import { authService } from '../services/authService';
import { NAV_GROUPS, SETTINGS_GROUP } from './navGroups';
import { filterNavGroupsByRoles } from './filterNavGroups';
import { NavGroup } from './types';

/**
 * Returns the sidebar groups the current user is allowed to see, in display order.
 *
 * Rules:
 *  - `Administrator` sees every group plus the Settings group.
 *  - Any other role sees only groups whose `allowedRoles` includes one of their roles.
 *  - If the user has no roles (or no logged-in user), returns an empty array — the
 *    sidebar should render nothing in that case (`ProtectedRoute` already redirects).
 *
 * Empty-item groups are still returned — the sidebar renders their header with a
 * "🚧 во изработка" affordance so the user can see IA progress group-by-group.
 *
 * The filtering is delegated to `filterNavGroupsByRoles` so it can be unit-tested
 * without React state.
 */
export function useNavForRoles(): NavGroup[] {
  return useMemo(() => {
    const user = authService.getCurrentUser();
    const roles = user?.roles ?? [];
    return filterNavGroupsByRoles(NAV_GROUPS, SETTINGS_GROUP, roles);
  }, []);
}
