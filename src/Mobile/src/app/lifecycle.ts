import { AppState, type AppStateStatus } from 'react-native';
import NetInfo from '@react-native-community/netinfo';
import { activateKeepAwakeAsync, deactivateKeepAwake } from 'expo-keep-awake';
import { drivingController } from '../features/driving/drivingController';
import { initProximityRealtime, resyncProximity } from '../features/proximity/proximityController';
import { voiceController } from '../features/voice/voiceController';
import { proximityHub } from '../services/signalr/proximityHub';
import { useAuthStore } from '../store/authStore';
import { useConnectivityStore } from '../store/connectivityStore';
import { useDrivingStore } from '../store/drivingStore';
import { useUiStore } from '../store/uiStore';

const KEEP_AWAKE_TAG = 'opdeweg-drive';
let started = false;

const sessionMissing = () => {
  void drivingController.recoverSession();
};

/**
 * App-wide lifecycle wiring: realtime events, app foreground/background, network transitions
 * and keep-awake. Each transition that could make proximity stale triggers a resync.
 */
export function startAppLifecycle(): void {
  if (started) {
    return;
  }

  started = true;
  voiceController.init();
  initProximityRealtime({
    onServerEndedSession: (ended) => void drivingController.handleServerEnded(ended),
    onSessionMissing: sessionMissing,
  });

  let appState: AppStateStatus = AppState.currentState;
  AppState.addEventListener('change', (next) => {
    const previous = appState;
    appState = next;
    const signedIn = useAuthStore.getState().status === 'signedIn';
    const driving = useDrivingStore.getState().status === 'active';

    if (next === 'active' && previous !== 'active' && signedIn) {
      void proximityHub.kick().then(() => proximityHub.start());
      if (driving) {
        void drivingController.sendNow('app_resume');
        void resyncProximity({ onSessionMissing: sessionMissing });
        voiceController.reconcile();
      }
    }

    // Not driving: no reason to hold a socket open in the background.
    if (next === 'background' && signedIn && !driving) {
      void proximityHub.stop();
    }
  });

  NetInfo.addEventListener((state) => {
    const online = state.isConnected !== false && state.isInternetReachable !== false;
    const connectivity = useConnectivityStore.getState();
    const wasOffline = connectivity.network === 'offline';
    connectivity.setNetwork(online ? 'online' : 'offline');

    if (online && wasOffline && useAuthStore.getState().status === 'signedIn') {
      void proximityHub.kick();
      if (useDrivingStore.getState().status === 'active') {
        void drivingController.sendNow('network_restored');
        voiceController.reconcile();
      }
    }
  });

  const syncKeepAwake = () => {
    const shouldKeepAwake = useUiStore.getState().keepScreenAwake && useDrivingStore.getState().status === 'active';
    if (shouldKeepAwake) {
      void activateKeepAwakeAsync(KEEP_AWAKE_TAG);
    } else {
      void deactivateKeepAwake(KEEP_AWAKE_TAG);
    }
  };
  useDrivingStore.subscribe(syncKeepAwake);
  useUiStore.subscribe(syncKeepAwake);
}
