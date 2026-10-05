import {
  HttpTransportType,
  type HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
  type RetryContext,
} from '@microsoft/signalr';
import { appConfig } from '../config';
import { tokenManager } from '../auth/tokenManager';
import { useConnectivityStore } from '../../store/connectivityStore';
import { backoffDelay } from '../../utils/backoff';
import { logger } from '../../utils/logger';
import type {
  DrivingSession,
  DrivingSessionEnded,
  ProximityGroup,
  ProximityGroupLeft,
  ProximitySnapshot,
  VoiceAccess,
} from '../../types/api';

/** Server → client events (mirrors IProximityClient on the backend). */
export interface ProximityHubEvents {
  ProximityGroupJoined: (group: ProximityGroup) => void;
  ProximityGroupLeft: (left: ProximityGroupLeft) => void;
  NearbyUsersChanged: (group: ProximityGroup) => void;
  DrivingSessionStarted: (session: DrivingSession) => void;
  DrivingSessionEnded: (ended: DrivingSessionEnded) => void;
}

/** Retry forever with capped, jittered backoff: drivers pass through tunnels and dead zones. */
const foreverRetry: IRetryPolicy = {
  nextRetryDelayInMilliseconds: (context: RetryContext) => backoffDelay(context.previousRetryCount, 1_000, 30_000),
};

class ProximityHubClient {
  private connection: HubConnection | null = null;
  private handlers: Partial<ProximityHubEvents> = {};
  private onResync: (() => void) | null = null;
  private restartTimer: ReturnType<typeof setTimeout> | null = null;
  private wanted = false;
  private attempts = 0;

  /** Event handlers and the post-(re)connect resync hook. */
  configure(handlers: Partial<ProximityHubEvents>, onResync: () => void): void {
    this.handlers = handlers;
    this.onResync = onResync;
  }

  get isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  async start(): Promise<void> {
    this.wanted = true;
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection ??= this.build();
    useConnectivityStore.getState().setRealtime('connecting');
    try {
      await this.connection.start();
      this.attempts = 0;
      useConnectivityStore.getState().setRealtime('connected');
      logger.info('realtime.connected');
      this.onResync?.();
    } catch (error) {
      logger.warn('realtime.start_failed', { error: String(error) });
      useConnectivityStore.getState().setRealtime('disconnected');
      this.scheduleRestart();
    }
  }

  async stop(): Promise<void> {
    this.wanted = false;
    if (this.restartTimer) {
      clearTimeout(this.restartTimer);
      this.restartTimer = null;
    }

    const connection = this.connection;
    this.connection = null;
    await connection?.stop().catch(() => undefined);
    useConnectivityStore.getState().setRealtime('disconnected');
  }

  /** Called when the network comes back: skip the remaining backoff. */
  async kick(): Promise<void> {
    if (this.wanted && !this.isConnected) {
      if (this.restartTimer) {
        clearTimeout(this.restartTimer);
        this.restartTimer = null;
      }

      await this.start();
    }
  }

  async getSnapshot(): Promise<ProximitySnapshot | null> {
    return this.isConnected ? this.connection!.invoke<ProximitySnapshot>('GetSnapshot') : null;
  }

  async requestVoiceToken(): Promise<VoiceAccess | null> {
    return this.isConnected ? this.connection!.invoke<VoiceAccess | null>('RequestVoiceToken') : null;
  }

  private build(): HubConnection {
    const connection = new HubConnectionBuilder()
      .withUrl(appConfig.hubUrl, {
        // React Native has native WebSockets; skipping negotiation avoids an extra round trip.
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
        accessTokenFactory: async () => (await tokenManager.getAccessToken()) ?? '',
      })
      .withAutomaticReconnect(foreverRetry)
      .withServerTimeout(45_000)
      .withKeepAliveInterval(15_000)
      .configureLogging(__DEV__ ? LogLevel.Warning : LogLevel.Error)
      .build();

    const forward = <K extends keyof ProximityHubEvents>(name: K) =>
      connection.on(name, (...args: Parameters<ProximityHubEvents[K]>) => {
        const handler = this.handlers[name] as ((...a: Parameters<ProximityHubEvents[K]>) => void) | undefined;
        handler?.(...args);
      });

    forward('ProximityGroupJoined');
    forward('ProximityGroupLeft');
    forward('NearbyUsersChanged');
    forward('DrivingSessionStarted');
    forward('DrivingSessionEnded');

    connection.onreconnecting(() => useConnectivityStore.getState().setRealtime('reconnecting'));
    connection.onreconnected(() => {
      useConnectivityStore.getState().setRealtime('connected');
      logger.info('realtime.reconnected');
      this.onResync?.();
    });
    connection.onclose(() => {
      useConnectivityStore.getState().setRealtime('disconnected');
      this.scheduleRestart();
    });

    return connection;
  }

  private scheduleRestart(): void {
    if (!this.wanted || this.restartTimer) {
      return;
    }

    const delay = backoffDelay(this.attempts++, 2_000, 30_000);
    this.restartTimer = setTimeout(() => {
      this.restartTimer = null;
      void this.start();
    }, delay);
  }
}

export const proximityHub = new ProximityHubClient();
