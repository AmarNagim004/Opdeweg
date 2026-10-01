import { useConnectivityStore } from '../store/connectivityStore';
import { useDrivingStore } from '../store/drivingStore';
import { nearbyCount, useProximityStore } from '../store/proximityStore';
import { useVoiceStore } from '../store/voiceStore';

/** Offline → Online → Driving → Nearby → Voice: the single state the UI is built around. */
export type JourneyPhase = 'offline' | 'online' | 'driving' | 'nearby' | 'voice';

export type VoiceSummary = 'off' | 'standby' | 'connecting' | 'live' | 'reconnecting' | 'retrying';

export interface Journey {
  phase: JourneyPhase;
  driving: boolean;
  transitioning: boolean;
  nearby: number;
  inGroup: boolean;
  voice: VoiceSummary;
  realtimeHealthy: boolean;
  offline: boolean;
}

export function useJourney(): Journey {
  const network = useConnectivityStore((s) => s.network);
  const realtime = useConnectivityStore((s) => s.realtime);
  const status = useDrivingStore((s) => s.status);
  const group = useProximityStore((s) => s.group);
  const nearbyList = useProximityStore((s) => s.nearby);
  const connection = useVoiceStore((s) => s.connection);

  const driving = status === 'active';
  const offline = network === 'offline';
  const nearby = nearbyCount({ group, nearby: nearbyList });

  let voice: VoiceSummary = 'off';
  if (driving) {
    voice = !group
      ? 'standby'
      : connection === 'connected'
        ? 'live'
        : connection === 'reconnecting'
          ? 'reconnecting'
          : connection === 'error'
            ? 'retrying'
            : 'connecting';
  }

  const phase: JourneyPhase = offline
    ? 'offline'
    : !driving
      ? 'online'
      : voice === 'live'
        ? 'voice'
        : nearby > 0
          ? 'nearby'
          : 'driving';

  return {
    phase,
    driving,
    transitioning: status === 'starting' || status === 'stopping',
    nearby,
    inGroup: group != null,
    voice,
    realtimeHealthy: realtime === 'connected',
    offline,
  };
}
