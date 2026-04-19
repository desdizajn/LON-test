import { useMemo } from 'react';
import { authService } from '../services/authService';
import { NAV_GROUPS, SETTINGS_GROUP } from './navGroups';
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
 */
export function useNavForRoles(): NavGroup[] {
  return useMemo(() => {
    const user = authService.getCurrentUser();
    const roles = user?.roles ?? [];

    if (roles.length === 0) return [];

    const isAdmin = roles.includes('Administrator');

    if (isAdmin) {
      return [...NAV_GROUPS, SETTINGS_GROUP];
    }

    const visible = NAV_GROUPS.filter((group) =>
      group.allowedRoles.some((allowed) => roles.includes(allowed))
    );

    return visible;
  }, []);
}
