import { Alert, StyleSheet, View } from 'react-native';
import { AppText } from '../../components/AppText';
import { Avatar } from '../../components/Avatar';
import { Button } from '../../components/Button';
import { Screen } from '../../components/Screen';
import { StatusDot } from '../../components/StatusDot';
import { useJourney } from '../../hooks/useJourney';
import { useVoiceStore } from '../../store/voiceStore';
import { useTheme } from '../../theme/ThemeProvider';
import { pluralize } from '../../utils/format';
import { drivingController } from '../driving/drivingController';
import { VoiceControls } from '../driving/components/VoiceControls';
import { SpeakerStage } from './components/SpeakerStage';

export function VoiceScreen() {
  const { colors, radius } = useTheme();
  const journey = useJourney();
  const participants = useVoiceStore((s) => s.participants);
  const localSpeaking = useVoiceStore((s) => s.localSpeaking);
  const muted = useVoiceStore((s) => s.muted);

  const speaker = participants.find((p) => p.isSpeaking) ?? null;
  const live = journey.voice === 'live';

  const [title, subtitle] = !journey.driving
    ? ['Voice is off', 'Start a drive to talk to drivers nearby']
    : speaker
      ? [speaker.name, 'Speaking']
      : live
        ? [localSpeaking && !muted ? 'You' : 'Quiet road', localSpeaking && !muted ? 'Speaking' : `${pluralize(participants.length, 'driver')} listening`]
        : journey.voice === 'standby'
          ? ['Standing by', 'You’ll be connected when drivers are near']
          : ['Connecting…', 'Joining nearby voice'];

  const chip = {
    off: ['Off', colors.textFaint],
    standby: ['Standby', colors.textFaint],
    connecting: ['Connecting', colors.warning],
    live: ['Live', colors.accent],
    reconnecting: ['Reconnecting', colors.warning],
    retrying: ['Retrying', colors.warning],
  }[journey.voice] as [string, string];

  return (
    <Screen>
      <View style={styles.header}>
        <AppText variant="title" accessibilityRole="header">
          Nearby voice
        </AppText>
        <View style={[styles.chip, { backgroundColor: colors.surface, borderRadius: radius.pill }]}>
          <StatusDot color={chip[1]} size={8} pulse={journey.voice === 'connecting' || journey.voice === 'reconnecting'} />
          <AppText variant="caption">{chip[0]}</AppText>
        </View>
      </View>

      <View style={styles.center}>
        <SpeakerStage speaker={speaker} title={title} subtitle={subtitle} />
      </View>

      {live && participants.length > 0 ? (
        <View style={styles.roster} accessibilityLabel={`${pluralize(participants.length, 'driver')} in channel`}>
          {participants.map((p) => (
            <View key={p.identity} style={styles.person}>
              <Avatar id={p.identity} name={p.name} size={44} speaking={p.isSpeaking} dimmed={p.isMuted} />
              <AppText variant="caption" tone={p.isSpeaking ? 'accent' : 'muted'} numberOfLines={1} style={styles.personName}>
                {p.name}
              </AppText>
            </View>
          ))}
        </View>
      ) : null}

      {journey.driving ? (
        <View style={styles.bottom}>
          <VoiceControls compact />
          <Button
            label="End drive"
            variant="danger"
            icon="stop-circle-outline"
            onPress={() =>
              Alert.alert('End drive?', 'You’ll leave nearby voice and stop sharing your location.', [
                { text: 'Keep driving', style: 'cancel' },
                { text: 'End drive', style: 'destructive', onPress: () => void drivingController.stop() },
              ])
            }
          />
        </View>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 12 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12, paddingVertical: 8 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  roster: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: 16 },
  person: { alignItems: 'center', width: 72, gap: 4 },
  personName: { maxWidth: 72 },
  bottom: { gap: 20 },
});
