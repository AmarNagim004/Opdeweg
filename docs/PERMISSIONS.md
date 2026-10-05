# Permissions & background behaviour

Opdeweg is meant to run with the phone locked in a mount or a pocket. iOS and Android differ a lot
here, so each is configured separately (`src/Mobile/app.config.ts`, config plugins and the local
module `src/Mobile/modules/opdeweg-drive-service`).

## iOS

| Item | Value | Why |
| --- | --- | --- |
| `NSLocationWhenInUseUsageDescription` | ✔ | Location during a drive. |
| `NSLocationAlwaysAndWhenInUseUsageDescription` | ✔ (optional grant) | Better recovery if iOS relaunches the app in the background. |
| `NSMicrophoneUsageDescription` | ✔ | Voice. |
| `UIBackgroundModes` | `location`, `audio` (+ `fetch`, added by expo-task-manager) | Keep receiving fixes and keep the voice session running while locked. |
| `NSAppTransportSecurity.NSAllowsLocalNetworking` | ✔ | Dev builds talk to a LAN API over HTTP. Release builds enforce HTTPS in code. |
| `NSCameraUsageDescription` | neutral text | Required by the WebRTC binary; the app never uses the camera. |

Behaviour:

- Location: `startLocationUpdatesAsync` with `BestForNavigation`, `activityType: AutomotiveNavigation`,
  `pausesUpdatesAutomatically: false`, `distanceFilter: none` and the blue background indicator.
  Started from the foreground (the user taps *Start*), it continues when locked with "While Using"
  permission; "Always" is requested optionally.
- Audio: `AVAudioSession` `playAndRecord` / `voiceChat` with `allowBluetooth` (HFP: car kits,
  motorcycle intercoms), `allowBluetoothA2DP`, `defaultToSpeaker` and `mixWithOthers` (navigation
  prompts stay audible). The session is activated at drive start and **kept active for the whole
  drive** (`deactivateOnStop: false`) so rooms can be re-joined while locked.
- Output: automatic routing (Bluetooth → wired → speaker); the Settings screen offers the system route
  picker and a "force loudspeaker" toggle.

> **Known iOS limitation.** iOS restricts *starting* microphone capture from the background. Keeping
> the audio session active for the whole drive covers the common case (rejoining after a group change
> while locked), but it is not guaranteed by Apple. The production-grade path is to represent a drive
> as a CallKit call (or adopt the PushToTalk framework); this is the top follow-up item.

## Android

| Permission | Type | Why |
| --- | --- | --- |
| `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION` | runtime | Location during a drive. |
| `ACCESS_BACKGROUND_LOCATION` | runtime, optional ("Altijd toestaan") | Recovery after process restarts; not needed while the drive service runs. |
| `RECORD_AUDIO`, `MODIFY_AUDIO_SETTINGS` | runtime / normal | Voice and audio routing. |
| `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `FOREGROUND_SERVICE_MICROPHONE` | normal | The drive service (Android 14+ requires typed services). |
| `POST_NOTIFICATIONS` | runtime (13+) | Shows the ongoing "Driving" notification. |
| `BLUETOOTH_CONNECT` | runtime (12+) | Detect and route to Bluetooth headsets / car kits. |
| `WAKE_LOCK`, `INTERNET`, `ACCESS_NETWORK_STATE` | normal | Voice and connectivity. |
| `CAMERA`, `SYSTEM_ALERT_WINDOW`, `RECORD_VIDEO`, external storage | **blocked** | Declared by libraries but not needed by an audio-only app. |

Behaviour:

- **Drive service.** One foreground service (`DriveForegroundService`, declared
  `foregroundServiceType="location|microphone"`) runs for the whole drive with a single ongoing
  notification. It is started from the foreground when the user taps *Start*, as Android 11+ requires
  for microphone/location "while-in-use" access, and its types are computed from the permissions that
  are actually granted — a revoked microphone degrades to location-only instead of crashing.
  While it runs the process counts as foreground, so fused-location updates (every ~2 s, time-based so
  a stationary car still sends heartbeats) keep flowing with the screen off. If the module is
  unavailable, expo-location's own foreground service is used as a fallback.
- **Audio.** LiveKit's communication preset (`MODE_IN_COMMUNICATION`), preferring
  Bluetooth → wired headset → speaker → earpiece, with `AUDIOFOCUS_GAIN_TRANSIENT_MAY_DUCK` so music
  ducks rather than stops.
- **OEM battery killers.** Some vendors still stop foreground services; document "disable battery
  optimisation" for affected devices.

## Lifecycle & failure matrix

| Event | Handling |
| --- | --- |
| Screen locked | Native location keeps running; JS throttle runs per callback (no timers); voice continues. |
| App backgrounded, not driving | SignalR socket closed to save battery. |
| App resumed | Forced fix (`app_resume`), snapshot resync, voice reconcile. |
| Network lost / regained | Backoff while offline; on regain: forced fix, SignalR reconnect, voice reconcile. |
| Bluetooth disconnects | OS re-routes (iOS `defaultToSpeaker`; Android AudioSwitch preference list). |
| GPS unavailable | Banner; server drops proximity after 60 s; session kept. |
| Location permission revoked | OS kills the app (both platforms). On next launch the drive resumes only if permissions are back; otherwise the Permissions screen explains. |
| Microphone permission revoked | Voice falls back to listen-only with a banner. |
| App killed during a drive | The persisted drive intent lets the headless location task keep reporting; the next launch resumes UI state, or the server ends the session after 30 min. |
