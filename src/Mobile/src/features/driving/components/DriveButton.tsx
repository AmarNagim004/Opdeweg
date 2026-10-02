import { ActivityIndicator, StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { AppText } from '../../../components/AppText';
import { PulseRings } from '../../../components/PulseRings';
import { ScalePressable } from '../../../components/Pressable';
import { t } from '../../../i18n/nl';
import { useTheme } from '../../../theme/ThemeProvider';

interface DriveButtonProps {
  /** Diameter in points; scaled to the screen by the caller. */
  size?: number;
  driving: boolean;
  busy: boolean;
  live: boolean;
  onPress: () => void;
}

/** The one primary action: a big, thumb-sized start/stop control with a live pulse while driving. */
export function DriveButton({ size = 216, driving, busy, live, onPress }: DriveButtonProps) {
  const { colors } = useTheme();
  const fill = driving ? colors.accent : colors.surface;
  const fg = driving ? colors.onAccent : colors.accentText;

  return (
    <View style={{ width: size * 1.45, height: size * 1.45, alignItems: 'center', justifyContent: 'center' }}>
      <PulseRings size={size} color={colors.accent} active={live} spread={1.4} />
      <ScalePressable
        onPress={onPress}
        disabled={busy}
        pressedScale={0.95}
        accessibilityRole="button"
        accessibilityLabel={driving ? t.drive.stopA11y : t.drive.startA11y}
        accessibilityHint={driving ? t.drive.stopHint : t.drive.startHint}
        accessibilityState={{ busy, checked: driving }}
        style={[styles.button, { width: size, height: size, borderRadius: size / 2, backgroundColor: fill, borderColor: colors.accent, shadowColor: colors.accent }]}
      >
        {busy ? (
          <ActivityIndicator size="large" color={fg} />
        ) : (
          <>
            <Ionicons name={driving ? 'navigate' : 'navigate-outline'} size={Math.round(size * 0.26)} color={fg} />
            <AppText variant="headline" style={{ color: fg, marginTop: 8 }}>
              {driving ? t.drive.stop : t.drive.start}
            </AppText>
          </>
        )}
      </ScalePressable>
    </View>
  );
}

const styles = StyleSheet.create({
  button: {
    borderWidth: 3,
    alignItems: 'center',
    justifyContent: 'center',
    shadowOpacity: 0.35,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: 0 },
    elevation: 8,
  },
});
