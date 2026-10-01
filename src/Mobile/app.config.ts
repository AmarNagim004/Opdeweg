import type { ConfigContext, ExpoConfig } from 'expo/config';

const IS_DEV = process.env.APP_VARIANT === 'development';

const LOCATION_WHEN_IN_USE =
  'Opdeweg uses your location during a drive to connect you with drivers within about 1 km. Other drivers only see a rounded distance, never your position.';
const LOCATION_ALWAYS =
  'Allow background location so Opdeweg keeps you connected with nearby drivers while your phone is locked during a drive. Tracking stops when you end the drive.';
const MICROPHONE =
  'Opdeweg uses the microphone so you can talk with drivers near you. It is only live while you are in a nearby voice channel, and you can mute at any time.';

export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: IS_DEV ? 'Opdeweg Dev' : 'Opdeweg',
  slug: 'opdeweg',
  scheme: 'opdeweg',
  version: '0.1.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  userInterfaceStyle: 'automatic',
  backgroundColor: '#08090B',
  ios: {
    bundleIdentifier: IS_DEV ? 'app.opdeweg.mobile.dev' : 'app.opdeweg.mobile',
    supportsTablet: false,
    infoPlist: {
      NSLocationWhenInUseUsageDescription: LOCATION_WHEN_IN_USE,
      NSLocationAlwaysAndWhenInUseUsageDescription: LOCATION_ALWAYS,
      NSMicrophoneUsageDescription: MICROPHONE,
      // location: keep receiving fixes while locked; audio: keep the voice session running.
      UIBackgroundModes: ['location', 'audio'],
      // Development builds talk to a local API over plain HTTP; release builds enforce HTTPS at runtime.
      NSAppTransportSecurity: { NSAllowsLocalNetworking: true },
      ITSAppUsesNonExemptEncryption: false,
    },
  },
  android: {
    package: IS_DEV ? 'app.opdeweg.mobile.dev' : 'app.opdeweg.mobile',
    adaptiveIcon: {
      backgroundColor: '#08090B',
      foregroundImage: './assets/android-icon-foreground.png',
      backgroundImage: './assets/android-icon-background.png',
      monochromeImage: './assets/android-icon-monochrome.png',
    },
    predictiveBackGestureEnabled: false,
    permissions: [
      'android.permission.ACCESS_COARSE_LOCATION',
      'android.permission.ACCESS_FINE_LOCATION',
      'android.permission.ACCESS_BACKGROUND_LOCATION',
      'android.permission.RECORD_AUDIO',
      'android.permission.MODIFY_AUDIO_SETTINGS',
      'android.permission.FOREGROUND_SERVICE',
      'android.permission.FOREGROUND_SERVICE_LOCATION',
      'android.permission.FOREGROUND_SERVICE_MICROPHONE',
      'android.permission.POST_NOTIFICATIONS',
      'android.permission.BLUETOOTH_CONNECT',
      'android.permission.WAKE_LOCK',
      'android.permission.INTERNET',
      'android.permission.ACCESS_NETWORK_STATE',
    ],
    // Audio-only app: drop permissions that WebRTC libraries declare by default.
    blockedPermissions: [
      'android.permission.CAMERA',
      'android.permission.SYSTEM_ALERT_WINDOW',
      'android.permission.RECORD_VIDEO',
      'android.permission.READ_EXTERNAL_STORAGE',
      'android.permission.WRITE_EXTERNAL_STORAGE',
    ],
  },
  plugins: [
    'expo-secure-store',
    [
      'expo-splash-screen',
      { image: './assets/splash-icon.png', imageWidth: 160, backgroundColor: '#F5F6F2', dark: { backgroundColor: '#08090B' } },
    ],
    [
      'expo-location',
      {
        locationWhenInUsePermission: LOCATION_WHEN_IN_USE,
        locationAlwaysAndWhenInUsePermission: LOCATION_ALWAYS,
        isIosBackgroundLocationEnabled: true,
        isAndroidBackgroundLocationEnabled: true,
        isAndroidForegroundServiceEnabled: true,
      },
    ],
    ['@livekit/react-native-expo-plugin', { android: { audioType: 'communication' } }],
    [
      '@config-plugins/react-native-webrtc',
      { microphonePermission: MICROPHONE, cameraPermission: 'Opdeweg is audio-only and does not use the camera.' },
    ],
    ['expo-build-properties', { android: { minSdkVersion: 26, usesCleartextTraffic: IS_DEV }, ios: { deploymentTarget: '16.4' } }],
  ],
  extra: {
    apiUrl: process.env.EXPO_PUBLIC_API_URL,
  },
});
