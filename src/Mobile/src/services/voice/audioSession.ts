import { Platform } from 'react-native';
import {
  AndroidAudioTypePresets,
  AudioSession,
  registerGlobals,
  setupIOSAudioManagement,
  type AppleAudioConfiguration,
} from '@livekit/react-native';
import { logger } from '../../utils/logger';

/**
 * iOS: play-and-record with voice processing, routed to Bluetooth HFP (car kits, motorcycle
 * intercoms, earbuds) or wired headsets when present and the loudspeaker otherwise. Mixing with
 * other audio keeps navigation prompts audible. The session is NOT deactivated between voice
 * groups: keeping it active for the whole drive lets the app re-join rooms while locked.
 */
const drivingAudio: AppleAudioConfiguration = {
  audioCategory: 'playAndRecord',
  audioCategoryOptions: ['allowBluetooth', 'allowBluetoothA2DP', 'defaultToSpeaker', 'mixWithOthers'],
  audioMode: 'voiceChat',
};

let initialised = false;

/** Call once at startup, before any LiveKit usage. */
export function initialiseVoiceRuntime(): void {
  if (initialised) {
    return;
  }

  initialised = true;
  registerGlobals({ autoConfigureAudioSession: false });

  if (Platform.OS === 'ios') {
    setupIOSAudioManagement(true, {
      recording: drivingAudio,
      recordingWithoutVoiceProcessing: { ...drivingAudio, audioMode: 'default' },
      playout: drivingAudio,
      deactivateOnStop: false,
    });
  }
}

/** Activates the platform audio session for the duration of a drive. */
export async function startDriveAudio(): Promise<void> {
  try {
    await AudioSession.configureAudio({
      android: {
        // Bluetooth (car / helmet intercom) first, then wired headset, then speaker.
        preferredOutputList: ['bluetooth', 'headset', 'speaker', 'earpiece'],
        audioTypeOptions: {
          ...AndroidAudioTypePresets.communication,
          // Duck music instead of pausing it while drivers talk.
          audioFocusMode: 'gainTransientMayDuck',
        },
      },
      ios: { defaultOutput: 'speaker' },
    });
    await AudioSession.startAudioSession();
  } catch (error) {
    logger.warn('audio.start_failed', { error: String(error) });
  }
}

export async function stopDriveAudio(): Promise<void> {
  await AudioSession.stopAudioSession().catch(() => undefined);
}

/** Forces the loudspeaker, or returns to automatic routing (Bluetooth / headset first). */
export async function setSpeakerOutput(forceSpeaker: boolean): Promise<void> {
  try {
    if (Platform.OS === 'ios') {
      await AudioSession.selectAudioOutput(forceSpeaker ? 'force_speaker' : 'default');
      return;
    }

    const outputs = await AudioSession.getAudioOutputs();
    const preferred = forceSpeaker
      ? 'speaker'
      : (['bluetooth', 'headset', 'speaker'].find((o) => outputs.includes(o)) ?? 'speaker');
    await AudioSession.selectAudioOutput(preferred);
  } catch (error) {
    logger.warn('audio.route_failed', { error: String(error) });
  }
}

/** iOS system route picker (AirPods, car, intercom, …). */
export async function showAudioRoutePicker(): Promise<void> {
  if (Platform.OS === 'ios') {
    await AudioSession.showAudioRoutePicker().catch(() => undefined);
  }
}

export const supportsRoutePicker = Platform.OS === 'ios';
