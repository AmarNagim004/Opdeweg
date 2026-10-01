import { proximityApi } from '../../services/api/endpoints';
import { proximityHub } from '../../services/signalr/proximityHub';
import { useDrivingStore } from '../../store/drivingStore';
import { useProximityStore } from '../../store/proximityStore';
import { logger } from '../../utils/logger';
import type { DrivingSessionEnded } from '../../types/api';

interface ProximityControllerDeps {
  onServerEndedSession: (ended: DrivingSessionEnded) => void;
  onSessionMissing: () => void;
}

/** Wires server-authoritative realtime events into the stores. */
export function initProximityRealtime(deps: ProximityControllerDeps): void {
  const proximity = () => useProximityStore.getState();

  proximityHub.configure(
    {
      ProximityGroupJoined: (group) => proximity().applyGroupJoined(group),
      ProximityGroupLeft: (left) => proximity().applyGroupLeft(left.groupId, left.reason),
      NearbyUsersChanged: (group) => proximity().applyRoster(group),
      DrivingSessionStarted: () => {
        // Started from another device; the next resync picks it up.
        void resyncProximity(deps);
      },
      DrivingSessionEnded: (ended) => deps.onServerEndedSession(ended),
    },
    () => void resyncProximity(deps),
  );
}

/** Full state resync (after reconnects, app resume, or suspected staleness). */
export async function resyncProximity(deps: Pick<ProximityControllerDeps, 'onSessionMissing'>): Promise<void> {
  if (useDrivingStore.getState().status !== 'active') {
    return;
  }

  try {
    const snapshot = (await proximityHub.getSnapshot()) ?? (await proximityApi.snapshot(true));
    if (!snapshot.sessionActive) {
      deps.onSessionMissing();
      return;
    }

    useProximityStore.getState().applySnapshot(snapshot);
  } catch (error) {
    logger.debug('proximity.resync_failed', { error: String(error) });
  }
}
