import { StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTheme } from '../theme/ThemeProvider';
import { AppText } from './AppText';
import { ScalePressable } from './Pressable';

type Tone = 'info' | 'warning' | 'danger';

interface BannerProps {
  tone?: Tone;
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  actionLabel?: string;
  onAction?: () => void;
}

/** One-line, glanceable status message. Shown sparingly: only when something needs attention. */
export function Banner({ tone = 'info', icon, title, actionLabel, onAction }: BannerProps) {
  const { colors, radius } = useTheme();
  const fg = tone === 'danger' ? colors.danger : tone === 'warning' ? colors.warning : colors.info;
  const bg = tone === 'danger' ? colors.dangerSoft : tone === 'warning' ? colors.warningSoft : colors.surfaceRaised;

  return (
    <View accessibilityRole="alert" style={[styles.row, { backgroundColor: bg, borderRadius: radius.md }]}>
      <Ionicons name={icon} size={20} color={fg} />
      <AppText variant="label" style={[styles.title, { color: fg }]} numberOfLines={2}>
        {title}
      </AppText>
      {actionLabel && onAction ? (
        <ScalePressable onPress={onAction} accessibilityRole="button" style={[styles.action, { borderColor: fg, borderRadius: radius.pill }]}>
          <AppText variant="label" style={{ color: fg }}>
            {actionLabel}
          </AppText>
        </ScalePressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 12, paddingHorizontal: 16 },
  title: { flex: 1 },
  action: { borderWidth: 1, paddingHorizontal: 14, paddingVertical: 8 },
});
