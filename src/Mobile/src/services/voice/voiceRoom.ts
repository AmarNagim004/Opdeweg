import {
  AudioPresets,
  ConnectionState,
  DisconnectReason,
  MediaDeviceFailure,
  type Participant,
  type RemoteParticipant,
  Room,
  RoomEvent,
} from 'livekit-client';
import type { VoiceAccess } from '../../types/api';
import { logger } from '../../utils/logger';
import { t } from '../../i18n/nl';

export interface VoiceRoomParticipant {
  identity: string;
  name: string;
  isSpeaking: boolean;
  isMuted: boolean;
}

export type VoiceRoomState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface VoiceRoomCallbacks {
  onState(state: VoiceRoomState): void;
  onParticipants(participants: VoiceRoomParticipant[], localSpeaking: boolean): void;
  onMicrophoneIssue(issue: 'permission' | 'unavailable'): void;
  /** The SFU dropped us without us asking (kicked, room closed, network gave up). */
  onUnexpectedDisconnect(reason: DisconnectReason | undefined): void;
}

/**
 * One LiveKit room at a time. The microphone track is published once per room and then muted /
 * unmuted in place (no re-acquiring the mic, no Bluetooth profile flapping).
 */
export class VoiceRoom {
  private room: Room | null = null;
  private roomName: string | null = null;
  private intentionalDisconnect = false;

  constructor(private readonly callbacks: VoiceRoomCallbacks) {}

  get currentRoom(): string | null {
    return this.roomName;
  }

  get isConnected(): boolean {
    return this.room?.state === ConnectionState.Connected;
  }

  async connect(access: VoiceAccess, muted: boolean): Promise<void> {
    if (this.roomName === access.room && this.room && this.room.state !== ConnectionState.Disconnected) {
      return;
    }

    await this.disconnect();

    const room = new Room({
      adaptiveStream: false,
      dynacast: false,
      disconnectOnPageLeave: false,
      stopLocalTrackOnUnpublish: true,
      audioCaptureDefaults: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      publishDefaults: { audioPreset: AudioPresets.speech, dtx: true, red: true, stopMicTrackOnMute: false },
    });

    this.room = room;
    this.roomName = access.room;
    this.intentionalDisconnect = false;
    this.wire(room);
    this.callbacks.onState('connecting');

    await room.connect(access.url, access.token, { autoSubscribe: true });
    logger.info('voice.connected', { room: access.room });

    try {
      await room.localParticipant.setMicrophoneEnabled(true);
      if (muted) {
        await room.localParticipant.setMicrophoneEnabled(false);
      }
    } catch (error) {
      // Listen-only fallback: hearing nearby drivers is still valuable without a microphone.
      const failure = MediaDeviceFailure.getFailure(error);
      this.callbacks.onMicrophoneIssue(failure === MediaDeviceFailure.PermissionDenied ? 'permission' : 'unavailable');
    }

    this.emitParticipants();
  }

  async setMuted(muted: boolean): Promise<void> {
    if (this.room?.state === ConnectionState.Connected) {
      await this.room.localParticipant.setMicrophoneEnabled(!muted);
    }
  }

  async disconnect(): Promise<void> {
    const room = this.room;
    this.room = null;
    this.roomName = null;
    if (room) {
      this.intentionalDisconnect = true;
      room.removeAllListeners();
      await room.disconnect().catch(() => undefined);
      logger.info('voice.disconnected');
    }

    this.callbacks.onParticipants([], false);
    this.callbacks.onState('disconnected');
  }

  private wire(room: Room): void {
    const refresh = () => this.emitParticipants();

    room
      .on(RoomEvent.ConnectionStateChanged, (state) => {
        if (state === ConnectionState.Connected) {
          this.callbacks.onState('connected');
        } else if (state === ConnectionState.Reconnecting || state === ConnectionState.SignalReconnecting) {
          this.callbacks.onState('reconnecting');
        } else if (state === ConnectionState.Connecting) {
          this.callbacks.onState('connecting');
        }
      })
      .on(RoomEvent.Disconnected, (reason) => {
        if (this.room !== room || this.intentionalDisconnect) {
          return;
        }

        this.room = null;
        this.roomName = null;
        this.callbacks.onParticipants([], false);
        this.callbacks.onState('disconnected');
        logger.warn('voice.dropped', { reason: reason ?? 'unknown' });
        this.callbacks.onUnexpectedDisconnect(reason);
      })
      .on(RoomEvent.ParticipantConnected, refresh)
      .on(RoomEvent.ParticipantDisconnected, refresh)
      .on(RoomEvent.ActiveSpeakersChanged, refresh)
      .on(RoomEvent.TrackMuted, refresh)
      .on(RoomEvent.TrackUnmuted, refresh)
      .on(RoomEvent.TrackSubscribed, refresh)
      .on(RoomEvent.MediaDevicesError, (error) => {
        const failure = MediaDeviceFailure.getFailure(error);
        this.callbacks.onMicrophoneIssue(failure === MediaDeviceFailure.PermissionDenied ? 'permission' : 'unavailable');
      });
  }

  private emitParticipants(): void {
    const room = this.room;
    if (!room) {
      return;
    }

    const remote = [...room.remoteParticipants.values()].map(toParticipant);
    this.callbacks.onParticipants(remote, room.localParticipant.isSpeaking);
  }
}

function toParticipant(p: RemoteParticipant | Participant): VoiceRoomParticipant {
  return {
    identity: p.identity,
    name: p.name || t.anonymousName,
    isSpeaking: p.isSpeaking,
    isMuted: !p.isMicrophoneEnabled,
  };
}

export { DisconnectReason };
