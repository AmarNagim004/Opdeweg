import { StyleSheet, View } from 'react-native';
import { AppText } from '../../../components/AppText';
import { Avatar } from '../../../components/Avatar';
import { PulseRings } from '../../../components/PulseRings';
import { useTheme } from '../../../theme/ThemeProvider';

const SIZE = 132;

interface SpeakerStageProps {
  speaker: { identity: string; name: string } | null;
  title: string;
  subtitle: string;
}

/** Who is talking right now, readable at arm's length. */
export function SpeakerStage({ speaker, title, subtitle }: SpeakerStageProps) {
  const { colors } = useTheme();
  return (
    <View style={styles.stage} accessibilityLiveRegion="polite" accessible accessibilityLabel={`${title}. ${subtitle}`}>
      <View style={styles.orb}>
        <PulseRings size={SIZE + 12} color={colors.accent} active={speaker != null} rings={2} />
        {speaker ? (
          <Avatar id={speaker.identity} name={speaker.name} size={SIZE} speaking />
        ) : (
          <View style={[styles.idle, { borderColor: colors.border, backgroundColor: colors.surface }]}>
            <View style={[styles.idleDot, { backgroundColor: colors.textFaint }]} />
          </View>
        )}
      </View>
      <AppText variant="title" align="center" numberOfLines={1}>
        {title}
      </AppText>
      <AppText variant="bodyStrong" tone={speaker ? 'accent' : 'muted'} align="center">
        {subtitle}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  stage: { alignItems: 'center', gap: 6 },
  orb: { width: SIZE * 1.7, height: SIZE * 1.7, alignItems: 'center', justifyContent: 'center' },
  idle: { width: SIZE, height: SIZE, borderRadius: SIZE / 2, borderWidth: 2, alignItems: 'center', justifyContent: 'center' },
  idleDot: { width: 14, height: 14, borderRadius: 7 },
});
