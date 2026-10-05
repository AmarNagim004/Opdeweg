import { Alert, StyleSheet, View } from 'react-native';
import { AppText } from '../../components/AppText';
import { Avatar } from '../../components/Avatar';
import { Button } from '../../components/Button';
import { Screen } from '../../components/Screen';
import { StatusDot } from '../../components/StatusDot';
import { useJourney } from '../../hooks/useJourney';
import { useVoiceStore } from '../../store/voiceStore';
import { useTheme } from '../../theme/ThemeProvider';
import { t } from '../../i18n/nl';
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
    ? [t.voice.offTitle, t.voice.offBody]
    : speaker
      ? [speaker.name, t.voice.speaking]
      : live
        ? [localSpeaking && !muted ? t.voice.you : t.voice.quietTitle, localSpeaking && !muted ? t.voice.speaking : t.voice.listening(participants.length)]
        : journey.voice === 'standby'
          ? [t.voice.standbyTitle, t.voice.standbyBody]
          : [t.voice.connectingTitle, t.voice.connectingBody];

  const chip = {
    off: [t.voiceState.off, colors.textFaint],
    standby: [t.voiceState.standby, colors.textFaint],
    connecting: [t.voiceState.connecting, colors.warning],
    live: [t.voiceState.live, colors.accent],
    reconnecting: [t.voiceState.reconnecting, colors.warning],
    retrying: [t.voiceState.retrying, colors.warning],
  }[journey.voice] as [string, string];

  return (
    <Screen>
      <View style={styles.header}>
        <AppText variant="title" accessibilityRole="header">
          {t.voice.title}
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
        <View style={styles.roster} accessibilityLabel={t.voice.inChannelA11y(participants.length)}>
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
            label={t.voice.stopDrive}
            variant="danger"
            icon="stop-circle-outline"
            onPress={() =>
              Alert.alert(t.drive.confirmStop.title, t.drive.confirmStop.body, [
                { text: t.drive.confirmStop.keepDriving, style: 'cancel' },
                { text: t.drive.confirmStop.stop, style: 'destructive', onPress: () => void drivingController.stop() },
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
