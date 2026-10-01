import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { Banner } from '../../../components/Banner';
import { useJourney } from '../../../hooks/useJourney';
import { openAppSettings } from '../../../services/location/permissions';
import { useConnectivityStore } from '../../../store/connectivityStore';
import { useDrivingStore } from '../../../store/drivingStore';
import { useLocationStore } from '../../../store/locationStore';
import { useVoiceStore } from '../../../store/voiceStore';
import type { RootStackParamList } from '../../../navigation/types';

/** At most one banner, by priority — enough to explain the state without overwhelming the driver. */
export function StatusBanner() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const journey = useJourney();
  const gps = useLocationStore((s) => s.gps);
  const micIssue = useVoiceStore((s) => s.micIssue);
  const lastEndReason = useDrivingStore((s) => s.lastEndReason);
  const backendReachable = useConnectivityStore((s) => s.backendReachable);
  const permission = useLocationStore((s) => s.permission);

  if (journey.offline) {
    return <Banner tone="danger" icon="cloud-offline" title={journey.driving ? 'No connection — we’ll reconnect automatically' : 'You’re offline'} />;
  }

  if (journey.driving && !backendReachable) {
    return <Banner tone="warning" icon="server-outline" title="Can’t reach Opdeweg — retrying" />;
  }

  if (journey.driving && gps === 'unavailable') {
    return <Banner tone="warning" icon="locate-outline" title="Waiting for GPS…" actionLabel="Settings" onAction={() => void openAppSettings()} />;
  }

  if (journey.driving && gps === 'weak') {
    return <Banner tone="info" icon="locate-outline" title="Weak GPS — nearby matching paused" />;
  }

  if (journey.driving && micIssue !== 'none') {
    return (
      <Banner
        tone="warning"
        icon="mic-off-outline"
        title={micIssue === 'permission' ? 'Microphone off — listening only' : 'Microphone unavailable — listening only'}
        actionLabel={micIssue === 'permission' ? 'Allow' : undefined}
        onAction={micIssue === 'permission' ? () => void openAppSettings() : undefined}
      />
    );
  }

  if (!journey.driving && lastEndReason === 'idle') {
    return <Banner tone="info" icon="time-outline" title="Your last drive ended after a long time without signal" />;
  }

  if (!journey.driving && lastEndReason === 'signedOut') {
    return <Banner tone="info" icon="log-out-outline" title="Drive ended because you signed out on another device" />;
  }

  if (journey.driving && permission === 'denied') {
    return <Banner tone="danger" icon="location-outline" title="Location access is off" actionLabel="Fix" onAction={() => navigation.navigate('Permissions')} />;
  }

  return journey.driving && !journey.realtimeHealthy ? <Banner tone="info" icon="sync-outline" title="Reconnecting live updates…" /> : null;
}
