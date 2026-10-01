import { Platform } from 'react-native';
import * as Haptics from 'expo-haptics';
import { ApiError } from '../../services/api/http';
import { drivingApi } from '../../services/api/endpoints';
import { driveService } from '../../services/background/driveService';
import { driveIntent } from '../../services/location/driveIntent';
import { locationReporter } from '../../services/location/locationReporter';
import {
  getCurrentSample,
  locationServicesEnabled,
  startLocationUpdates,
  stopLocationUpdates,
} from '../../services/location/locationService';
import type { ForcedReason } from '../../services/location/locationThrottle';
import { readPermissions } from '../../services/location/permissions';
import { proximityHub } from '../../services/signalr/proximityHub';
import { startDriveAudio, stopDriveAudio } from '../../services/voice/audioSession';
import { useDrivingStore } from '../../store/drivingStore';
import { useLocationStore } from '../../store/locationStore';
import { useProximityStore } from '../../store/proximityStore';
import { useUiStore } from '../../store/uiStore';
import { useVoiceStore } from '../../store/voiceStore';
import { logger } from '../../utils/logger';
import { voiceController } from '../voice/voiceController';
import type { DrivingSessionEnded, DrivingSessionEndReason, DrivingSession } from '../../types/api';

export type StartDriveResult = 'started' | 'needsPermissions' | 'locationServicesOff' | 'offline' | 'error';

const RECOVERY_COOLDOWN_MS = 30_000;

class DrivingController {
  private lastRecoveryAttempt = 0;

  constructor() {
    locationReporter.setSessionLostHandler(() => this.recoverSession());
  }

  /** User tapped "Start driving". */
  async start(): Promise<StartDriveResult> {
    const store = useDrivingStore.getState();
    if (store.status === 'active' || store.status === 'starting') {
      return 'started';
    }

    const permissions = await readPermissions();
    if (permissions.location !== 'granted' || permissions.microphone !== 'granted') {
      return 'needsPermissions';
    }

    if (!(await locationServicesEnabled())) {
      return 'locationServicesOff';
    }

    store.setStatus('starting');
    try {
      const session = await drivingApi.start();
      useVoiceStore.getState().setMuted(useUiStore.getState().startMuted);
      await this.bringUp(session, 'session_start');
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      return 'started';
    } catch (error) {
      logger.warn('drive.start_failed', { error: String(error) });
      await this.tearDown();
      // If the server session was created but the device could not start tracking, close it again.
      void drivingApi.end().catch(() => undefined);
      useDrivingStore.getState().setStatus('idle');
      return error instanceof ApiError && error.isNetworkError ? 'offline' : 'error';
    }
  }

  /** User ended the drive. Local teardown never waits on the network. */
  async stop(): Promise<void> {
    if (useDrivingStore.getState().status === 'idle') {
      return;
    }

    useDrivingStore.getState().setStatus('stopping');
    await this.tearDown();
    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    try {
      await drivingApi.end();
    } catch {
      // Offline: the server expires the session on its own (idle timeout).
    }

    useDrivingStore.getState().setEnded('userEnded');
  }

  /** The server ended the session (idle timeout, other device, sign-out elsewhere). */
  async handleServerEnded(ended: DrivingSessionEnded): Promise<void> {
    if (useDrivingStore.getState().status !== 'active') {
      return;
    }

    await this.tearDown();
    useDrivingStore.getState().setEnded(ended.reason);
  }

  /** Called when the API says there is no active session although we think we are driving. */
  async recoverSession(): Promise<boolean> {
    if (!(await driveIntent.get())) {
      await stopLocationUpdates().catch(() => undefined);
      return false;
    }

    const now = Date.now();
    if (now - this.lastRecoveryAttempt < RECOVERY_COOLDOWN_MS) {
      return false;
    }

    this.lastRecoveryAttempt = now;
    try {
      const session = await drivingApi.start(); // idempotent
      useDrivingStore.getState().setActive(session);
      logger.info('drive.session_recovered');
      return true;
    } catch {
      return false;
    }
  }

  /** On launch / sign-in: continue a drive that was running when the app was closed. */
  async resumeIfNeeded(): Promise<void> {
    if (!(await driveIntent.get())) {
      await stopLocationUpdates().catch(() => undefined);
      return;
    }

    try {
      const current = await drivingApi.current();
      if (!current) {
        await this.tearDown();
        useDrivingStore.getState().setEnded('idle' satisfies DrivingSessionEndReason);
        return;
      }

      await this.bringUp(current, 'session_recovered');
    } catch (error) {
      // Offline at launch: keep tracking; the reporter recovers once the network is back.
      logger.info('drive.resume_deferred', { error: String(error) });
    }
  }

  /** Force-send a fresh fix (app resumed, network restored, ...). */
  async sendNow(reason: ForcedReason): Promise<void> {
    if (useDrivingStore.getState().status !== 'active') {
      return;
    }

    const sample = await getCurrentSample();
    if (sample) {
      await locationReporter.handleSample(sample, reason);
    } else {
      useLocationStore.getState().setGps('unavailable');
    }
  }

  private async bringUp(session: DrivingSession, reason: ForcedReason): Promise<void> {
    await driveIntent.set(true);
    const ownService = await driveService.start();
    await startDriveAudio();
    await startLocationUpdates({ needsOwnForegroundService: Platform.OS === 'android' && !ownService });
    useDrivingStore.getState().setActive(session);
    useLocationStore.getState().setGps('searching');
    locationReporter.reset();
    void proximityHub.start();
    void this.sendNow(reason);
  }

  private async tearDown(): Promise<void> {
    await driveIntent.set(false);
    await voiceController.disconnect();
    await stopLocationUpdates().catch(() => undefined);
    await driveService.stop();
    await stopDriveAudio();
    useProximityStore.getState().reset();
    useLocationStore.getState().reset();
    locationReporter.reset();
  }
}

export const drivingController = new DrivingController();
