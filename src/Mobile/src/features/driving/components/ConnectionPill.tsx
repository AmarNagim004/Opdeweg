import { StyleSheet, View } from 'react-native';
import { AppText } from '../../../components/AppText';
import { StatusDot } from '../../../components/StatusDot';
import { useJourney } from '../../../hooks/useJourney';
import { useConnectivityStore } from '../../../store/connectivityStore';
import { useTheme } from '../../../theme/ThemeProvider';

export function ConnectionPill() {
  const { colors, radius } = useTheme();
  const journey = useJourney();
  const realtime = useConnectivityStore((s) => s.realtime);
  const backendReachable = useConnectivityStore((s) => s.backendReachable);

  const [label, color, pulse] = journey.offline
    ? ['Offline', colors.danger, false]
    : !backendReachable
      ? ['Server unreachable', colors.warning, true]
      : realtime === 'connected'
        ? ['Connected', colors.accent, false]
        : ['Connecting', colors.warning, true];

  return (
    <View accessibilityRole="text" accessibilityLabel={`Connection: ${label}`} style={[styles.pill, { backgroundColor: colors.surface, borderRadius: radius.pill, borderColor: colors.border }]}>
      <StatusDot color={color} size={8} pulse={pulse} />
      <AppText variant="caption">{label}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  pill: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12, paddingVertical: 8, borderWidth: StyleSheet.hairlineWidth },
});
