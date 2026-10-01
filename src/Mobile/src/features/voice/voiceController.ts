import { ApiError } from '../../services/api/http';
import { proximityApi } from '../../services/api/endpoints';
import { proximityHub } from '../../services/signalr/proximityHub';
import { DisconnectReason, VoiceRoom } from '../../services/voice/voiceRoom';
import { useDrivingStore } from '../../store/drivingStore';
import { useProximityStore } from '../../store/proximityStore';
import { useVoiceStore } from '../../store/voiceStore';
import { backoffDelay } from '../../utils/backoff';
import { logger } from '../../utils/logger';
import type { VoiceAccess } from '../../types/api';

/**
 * Keeps the LiveKit connection in line with the server-assigned proximity group:
 * group assigned → join its room; group gone or drive ended → leave. Never user-driven.
 */
class VoiceController {
  private readonly room = new VoiceRoom({
    onState: (state) => {
      const store = useVoiceStore.getState();
      if (state === 'disconnected') {
        store.setConnection('idle', null);
      } else {
        store.setConnection(state, this.room.currentRoom);
      }
    },
    onParticipants: (participants, localSpeaking) => {
      useVoiceStore.getState().setParticipants(participants);
      useVoiceStore.getState().setLocalSpeaking(localSpeaking);
    },
    onMicrophoneIssue: (issue) => useVoiceStore.getState().setMicIssue(issue),
    onUnexpectedDisconnect: (reason) => this.handleDrop(reason),
  });

  private connectedGroupId: string | null = null;
  private queue: Promise<void> = Promise.resolve();
  private retryAttempt = 0;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private initialised = false;

  init(): void {
    if (this.initialised) {
      return;
    }

    this.initialised = true;
    useProximityStore.subscribe((state, previous) => {
      if (state.group?.groupId !== previous.group?.groupId) {
        this.reconcile();
      }
    });
    useDrivingStore.subscribe((state, previous) => {
      if (state.status !== previous.status) {
        this.reconcile();
      }
    });
  }

  /** Serialised so overlapping group changes never race two connects. */
  reconcile(): void {
    this.queue = this.queue.then(() => this.doReconcile()).catch((error: unknown) => {
      logger.warn('voice.reconcile_failed', { error: String(error) });
    });
  }

  async setMuted(muted: boolean): Promise<void> {
    useVoiceStore.getState().setMuted(muted);
    await this.room.setMuted(muted).catch(() => undefined);
  }

  async disconnect(): Promise<void> {
    this.clearRetry();
    this.connectedGroupId = null;
    await this.queue.catch(() => undefined);
    await this.room.disconnect();
    useVoiceStore.getState().reset();
  }

  private async doReconcile(): Promise<void> {
    const driving = useDrivingStore.getState().status === 'active';
    const group = useProximityStore.getState().group;

    if (!driving || !group) {
      this.clearRetry();
      if (this.connectedGroupId || this.room.currentRoom) {
        this.connectedGroupId = null;
        await this.room.disconnect();
      }

      return;
    }

    if (this.connectedGroupId === group.groupId && this.room.currentRoom) {
      return;
    }

    const access = await this.ensureFreshAccess(group.groupId, group.voice);
    if (!access || useProximityStore.getState().group?.groupId !== group.groupId) {
      return;
    }

    try {
      useVoiceStore.getState().setMicIssue('none');
      this.connectedGroupId = group.groupId;
      await this.room.connect(access, useVoiceStore.getState().muted);
      this.retryAttempt = 0;
    } catch (error) {
      this.connectedGroupId = null;
      logger.warn('voice.connect_failed', { error: String(error) });
      useVoiceStore.getState().setConnection('error', null);
      // The token may be stale; force a fresh one on retry.
      useProximityStore.setState((s) => (s.group ? { group: { ...s.group, voice: null } } : s));
      this.scheduleRetry();
    }
  }

  private async ensureFreshAccess(groupId: string, current: VoiceAccess | null): Promise<VoiceAccess | null> {
    if (current && Date.parse(current.expiresAt) - Date.now() > 15_000) {
      return current;
    }

    try {
      const access = (await proximityHub.requestVoiceToken().catch(() => null)) ?? (await proximityApi.voiceToken());
      useProximityStore.getState().setVoiceAccess(groupId, access);
      return access;
    } catch (error) {
      if (error instanceof ApiError && error.code === 'not_in_group') {
        // Our view was stale; the server no longer has us in that group.
        useProximityStore.getState().applyGroupLeft(groupId, 'outOfRange');
        return null;
      }

      this.scheduleRetry();
      return null;
    }
  }

  private handleDrop(reason: DisconnectReason | undefined): void {
    this.connectedGroupId = null;
    if (reason === DisconnectReason.PARTICIPANT_REMOVED || reason === DisconnectReason.ROOM_DELETED) {
      // The server moved us out; proximity events / the next snapshot tell us where to go.
      return;
    }

    useProximityStore.setState((s) => (s.group ? { group: { ...s.group, voice: null } } : s));
    this.scheduleRetry();
  }

  private scheduleRetry(): void {
    if (this.retryTimer) {
      return;
    }

    const delay = backoffDelay(this.retryAttempt++, 1_000, 20_000);
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      this.reconcile();
    }, delay);
  }

  private clearRetry(): void {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }

    this.retryAttempt = 0;
  }
}

export const voiceController = new VoiceController();
