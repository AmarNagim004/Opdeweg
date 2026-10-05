import { Platform } from 'react-native';
import { requireOptionalNativeModule } from 'expo';

export interface DriveNotificationOptions {
  title: string;
  body: string;
}

interface DriveServiceNativeModule {
  start(options: DriveNotificationOptions): Promise<void>;
  stop(): Promise<void>;
  isRunning(): boolean;
}

// Optional so the JS bundle still runs in environments without the native module (tests, Expo Go).
const native = requireOptionalNativeModule<DriveServiceNativeModule>('OpdewegDriveService');

/** True when the platform relies on this module for background execution (Android). */
export const isDriveServiceAvailable = Platform.OS === 'android' && native != null;

export async function startDriveService(options: DriveNotificationOptions): Promise<void> {
  await native?.start(options);
}

export async function stopDriveService(): Promise<void> {
  await native?.stop();
}

export function isDriveServiceRunning(): boolean {
  return native?.isRunning() ?? false;
}
