import * as Location from 'expo-location';
import { Linking, PermissionsAndroid, Platform } from 'react-native';
import { permissions as mediaPermissions } from '@livekit/react-native-webrtc';

export type PermissionStatus = 'granted' | 'denied' | 'undetermined' | 'notApplicable';

export interface PermissionSnapshot {
  location: PermissionStatus;
  backgroundLocation: PermissionStatus;
  microphone: PermissionStatus;
  notifications: PermissionStatus;
  bluetooth: PermissionStatus;
}

const fromExpo = (status: Location.PermissionStatus): PermissionStatus =>
  status === Location.PermissionStatus.GRANTED ? 'granted' : status === Location.PermissionStatus.DENIED ? 'denied' : 'undetermined';

async function androidStatus(permission: (typeof PermissionsAndroid.PERMISSIONS)[keyof typeof PermissionsAndroid.PERMISSIONS], minApi: number) {
  if (Platform.OS !== 'android' || (Platform.Version as number) < minApi) {
    return 'notApplicable' as const;
  }

  return (await PermissionsAndroid.check(permission)) ? ('granted' as const) : ('undetermined' as const);
}

export async function readPermissions(): Promise<PermissionSnapshot> {
  const [foreground, background, microphone, notifications, bluetooth] = await Promise.all([
    Location.getForegroundPermissionsAsync(),
    Location.getBackgroundPermissionsAsync(),
    mediaPermissions.query({ name: 'microphone' }) as Promise<string>,
    androidStatus(PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS, 33),
    androidStatus(PermissionsAndroid.PERMISSIONS.BLUETOOTH_CONNECT, 31),
  ]);

  return {
    location: fromExpo(foreground.status),
    backgroundLocation: fromExpo(background.status),
    microphone: microphone === 'granted' ? 'granted' : microphone === 'denied' ? 'denied' : 'undetermined',
    notifications,
    bluetooth,
  };
}

export async function requestForegroundLocation(): Promise<boolean> {
  const result = await Location.requestForegroundPermissionsAsync();
  return result.granted;
}

/**
 * "Always" location. Optional: a drive started in the foreground keeps receiving updates when
 * locked (iOS background indicator / Android foreground service), but "Always" makes recovery
 * after the OS relaunches the app more reliable.
 */
export async function requestBackgroundLocation(): Promise<boolean> {
  const result = await Location.requestBackgroundPermissionsAsync();
  return result.granted;
}

export async function requestMicrophone(): Promise<boolean> {
  return Boolean(await mediaPermissions.request({ name: 'microphone' }));
}

/** Android extras: the drive notification (13+) and Bluetooth headset routing (12+). */
export async function requestAndroidExtras(): Promise<void> {
  if (Platform.OS !== 'android') {
    return;
  }

  const wanted = [
    (Platform.Version as number) >= 33 ? PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS : null,
    (Platform.Version as number) >= 31 ? PermissionsAndroid.PERMISSIONS.BLUETOOTH_CONNECT : null,
  ].filter((p): p is NonNullable<typeof p> => p != null);

  if (wanted.length > 0) {
    await PermissionsAndroid.requestMultiple(wanted);
  }
}

export const openAppSettings = () => Linking.openSettings();
