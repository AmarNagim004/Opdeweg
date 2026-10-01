import { ApiError } from '../api/http';
import { drivingApi } from '../api/endpoints';
import { useUiStore } from '../../store/uiStore';
import { useLocationStore } from '../../store/locationStore';
import { useProximityStore } from '../../store/proximityStore';
import { useConnectivityStore } from '../../store/connectivityStore';
import { backoffDelay } from '../../utils/backoff';
import { logger } from '../../utils/logger';
import {
  LocationThrottle,
  defaultThrottleConfig,
  type ForcedReason,
  type LocationSample,
  type ThrottleConfig,
} from './locationThrottle';
import type { LocationUpdate } from '../../types/api';

type SessionLostHandler = () => Promise<boolean>;

/**
 * Turns native location callbacks into server updates: throttles on-device (50 m / 15 s /
 * heading / speed), keeps one request in flight, backs off while offline, and mirrors the
 * server's proximity answer into the stores.
 */
class LocationReporter {
  private readonly throttle = new LocationThrottle(defaultThrottleConfig);
  private inFlight = false;
  private failures = 0;
  private retryNotBefore = 0;
  private onSessionLost: SessionLostHandler | null = null;

  /** Registered by the driving controller; returns true when the session was recovered. */
  setSessionLostHandler(handler: SessionLostHandler): void {
    this.onSessionLost = handler;
  }

  reset(): void {
    this.throttle.reset();
    this.failures = 0;
    this.retryNotBefore = 0;
  }

  async handleSample(sample: LocationSample, forced?: ForcedReason): Promise<void> {
    const now = Date.now();
    useLocationStore.getState().setFix(sample.accuracy, sample.timestamp);
    this.throttle.updateConfig(currentThrottleConfig());

    const decision = this.throttle.evaluate(sample, now, forced);
    if (!decision.transmit || this.inFlight || (!forced && now < this.retryNotBefore)) {
      return;
    }

    this.inFlight = true;
    try {
      const response = await drivingApi.postLocation(toUpdate(sample));
      this.throttle.markSent(sample, Date.now());
      this.failures = 0;
      this.retryNotBefore = 0;
      useLocationStore.getState().setSent(decision.reason, Date.now());
      useConnectivityStore.getState().setBackendReachable(true);
      if (response.proximity) {
        useProximityStore.getState().applySnapshot(response.proximity);
      }
    } catch (error) {
      await this.handleFailure(error, sample);
    } finally {
      this.inFlight = false;
    }
  }

  private async handleFailure(error: unknown, sample: LocationSample): Promise<void> {
    if (!(error instanceof ApiError)) {
      logger.warn('location.send_failed', { error: String(error) });
      return;
    }

    if (error.code === 'no_active_session') {
      // Server lost or expired the session (e.g. after a long dead zone). Resume if the user still drives.
      const recovered = (await this.onSessionLost?.()) ?? false;
      if (recovered) {
        this.throttle.reset();
      }

      return;
    }

    if (error.isNetworkError || error.status >= 500 || error.status === 429) {
      this.failures += 1;
      this.retryNotBefore = Date.now() + backoffDelay(this.failures, 2_000, 30_000);
      useConnectivityStore.getState().setBackendReachable(!error.isNetworkError && error.status < 500);
      return;
    }

    // Rejected fix (implausible, stale, out of order): don't resend it, wait for a fresh one.
    logger.info('location.rejected', { code: error.code });
    this.throttle.markSent(sample, Date.now());
  }
}

function currentThrottleConfig(): ThrottleConfig {
  const server = useUiStore.getState().serverConfig?.location;
  if (!server) {
    return defaultThrottleConfig;
  }

  return {
    ...defaultThrottleConfig,
    distanceThresholdMeters: server.distanceThresholdMeters,
    maxIntervalMs: server.maxIntervalSeconds * 1000,
    minIntervalMs: server.minIntervalMilliseconds,
    headingChangeDegrees: server.headingChangeDegrees,
    speedChangeMetersPerSecond: server.speedChangeMetersPerSecond,
  };
}

function toUpdate(sample: LocationSample): LocationUpdate {
  return {
    latitude: sample.latitude,
    longitude: sample.longitude,
    speed: sample.speed ?? undefined,
    heading: sample.heading ?? undefined,
    accuracy: sample.accuracy ?? undefined,
    timestamp: new Date(sample.timestamp).toISOString(),
  };
}

export const locationReporter = new LocationReporter();
