import { useMemo } from 'react';
import { StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { AppText } from '../../components/AppText';
import { Card } from '../../components/Card';
import { Screen } from '../../components/Screen';
import { StatusDot } from '../../components/StatusDot';
import { useJourney } from '../../hooks/useJourney';
import { useProximityStore } from '../../store/proximityStore';
import { useUiStore } from '../../store/uiStore';
import { useVoiceStore } from '../../store/voiceStore';
import { useTheme } from '../../theme/ThemeProvider';
import { formatDistance, pluralize } from '../../utils/format';
import { DriverRow } from './components/DriverRow';

export function NearbyScreen() {
  const { colors, radius } = useTheme();
  const journey = useJourney();
  const group = useProximityStore((s) => s.group);
  const nearby = useProximityStore((s) => s.nearby);
  const participants = useVoiceStore((s) => s.participants);
  const speaking = useMemo(() => new Set(participants.filter((p) => p.isSpeaking).map((p) => p.identity)), [participants]);
  const joinDistance = useUiStore((s) => s.serverConfig?.proximity.joinDistanceMeters ?? 1000);

  const members = group?.members ?? [];

  return (
    <Screen scroll>
      <AppText variant="title" accessibilityRole="header" style={styles.title}>
        Nearby
      </AppText>

      <View style={[styles.countPill, { backgroundColor: colors.surface, borderRadius: radius.pill }]}>
        <StatusDot color={journey.nearby > 0 ? colors.accent : colors.textFaint} pulse={journey.driving && journey.nearby === 0} />
        <AppText variant="label">{journey.driving ? `${pluralize(journey.nearby, 'driver')} nearby` : 'Not driving'}</AppText>
      </View>

      {!journey.driving ? (
        <EmptyState icon="car-sport-outline" title="Start a drive to see who’s around" body={`Drivers within ${formatDistance(joinDistance)} of you appear here automatically.`} />
      ) : journey.nearby === 0 ? (
        <EmptyState icon="radio-outline" title="No drivers nearby yet" body="We’ll connect you the moment someone is in range. No need to touch your phone." />
      ) : (
        <>
          {members.length > 0 ? (
            <Card padded={false} style={styles.section}>
              <AppText variant="overline" tone="muted" style={styles.sectionLabel}>
                In your voice channel
              </AppText>
              {members.map((driver, index) => (
                <View key={driver.id} style={index > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }}>
                  <DriverRow driver={driver} speaking={speaking.has(driver.id)} />
                </View>
              ))}
            </Card>
          ) : null}

          {nearby.length > 0 ? (
            <Card padded={false} style={styles.section}>
              <AppText variant="overline" tone="muted" style={styles.sectionLabel}>
                Also nearby
              </AppText>
              {nearby.map((driver, index) => (
                <View key={driver.id} style={index > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }}>
                  <DriverRow driver={driver} speaking={false} />
                </View>
              ))}
            </Card>
          ) : null}
        </>
      )}

      <View style={styles.note}>
        <Ionicons name="shield-checkmark-outline" size={16} color={colors.textMuted} />
        <AppText variant="caption" tone="muted" style={styles.noteText}>
          Distances are approximate. Exact locations are never shared with other drivers.
        </AppText>
      </View>
    </Screen>
  );
}

function EmptyState({ icon, title, body }: { icon: keyof typeof Ionicons.glyphMap; title: string; body: string }) {
  const { colors } = useTheme();
  return (
    <Card style={styles.empty}>
      <Ionicons name={icon} size={40} color={colors.accentText} />
      <AppText variant="headline" align="center">
        {title}
      </AppText>
      <AppText tone="muted" align="center">
        {body}
      </AppText>
    </Card>
  );
}

const styles = StyleSheet.create({
  title: { marginTop: 12 },
  countPill: { flexDirection: 'row', alignItems: 'center', gap: 10, alignSelf: 'flex-start', paddingHorizontal: 14, paddingVertical: 10 },
  section: { paddingHorizontal: 16, paddingTop: 16, paddingBottom: 4 },
  sectionLabel: { marginBottom: 4 },
  empty: { alignItems: 'center', gap: 10, paddingVertical: 36 },
  note: { flexDirection: 'row', gap: 8, alignItems: 'flex-start', paddingHorizontal: 4 },
  noteText: { flex: 1 },
});
