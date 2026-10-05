import { useCallback, useEffect, useState } from 'react';
import { AppState } from 'react-native';
import { readPermissions, type PermissionSnapshot } from '../services/location/permissions';

/** Live permission snapshot; refreshed when returning from the Settings app. */
export function usePermissions(): [PermissionSnapshot | null, () => Promise<void>] {
  const [snapshot, setSnapshot] = useState<PermissionSnapshot | null>(null);
  const refresh = useCallback(async () => setSnapshot(await readPermissions()), []);

  useEffect(() => {
    let active = true;
    const load = () =>
      void readPermissions().then((next) => {
        if (active) {
          setSnapshot(next);
        }
      });

    load();
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        load();
      }
    });
    return () => {
      active = false;
      subscription.remove();
    };
  }, []);

  return [snapshot, refresh];
}
