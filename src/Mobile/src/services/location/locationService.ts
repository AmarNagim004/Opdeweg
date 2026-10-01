import * as Location from 'expo-location';
import { LOCATION_TASK } from './locationTaskName';
import type { LocationSample } from './locationThrottle';

export function toSample(location: Location.LocationObject): LocationSample | null {
  // Never transmit mock-provider fixes from release builds: they are a trivial spoofing vector.
  if (location.mocked && !__DEV__) {
    return null;
  }

  const { latitude, longitude, speed, heading, accuracy } = location.coords;
  return {
    latitude,
    longitude,
    speed: speed != null && speed >= 0 ? speed : null,
    heading: heading != null && heading >= 0 ? heading : null,
    accuracy: accuracy ?? null,
    timestamp: location.timestamp,
  };
}

/**
 * Starts native, OS-driven location updates. Updates are requested time-based (not only on
 * movement) so a stationary driver keeps a liveness heartbeat; the JS throttle decides what
 * is actually sent. No JavaScript timers are involved, so this keeps working when locked.
 */
export async function startLocationUpdates(options: { needsOwnForegroundService: boolean }): Promise<void> {
  if (await Location.hasStartedLocationUpdatesAsync(LOCATION_TASK).catch(() => false)) {
    return;
  }

  await Location.startLocationUpdatesAsync(LOCATION_TASK, {
    accuracy: Location.Accuracy.BestForNavigation,
    timeInterval: 2_000,
    distanceInterval: 0,
    deferredUpdatesInterval: 0,
    deferredUpdatesDistance: 0,
    activityType: Location.ActivityType.AutomotiveNavigation,
    pausesUpdatesAutomatically: false,
    showsBackgroundLocationIndicator: true,
    // On Android our drive service (location|microphone) already keeps the process in the
    // foreground; expo-location's own service is only used as a fallback without it.
    foregroundService: options.needsOwnForegroundService
      ? {
          notificationTitle: 'Opdeweg · Driving',
          notificationBody: 'Sharing your approximate position with nearby drivers.',
          notificationColor: '#C8F03A',
          killServiceOnDestroy: true,
        }
      : undefined,
  });
}

export async function stopLocationUpdates(): Promise<void> {
  if (await Location.hasStartedLocationUpdatesAsync(LOCATION_TASK).catch(() => false)) {
    await Location.stopLocationUpdatesAsync(LOCATION_TASK);
  }
}

export async function getCurrentSample(): Promise<LocationSample | null> {
  try {
    const location = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
    return toSample(location);
  } catch {
    return null;
  }
}

export const locationServicesEnabled = () => Location.hasServicesEnabledAsync();
