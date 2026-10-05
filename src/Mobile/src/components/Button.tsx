import { ActivityIndicator, StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTheme } from '../theme/ThemeProvider';
import { AppText } from './AppText';
import { ScalePressable } from './Pressable';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: Variant;
  icon?: keyof typeof Ionicons.glyphMap;
  loading?: boolean;
  disabled?: boolean;
  accessibilityHint?: string;
}

export function Button({ label, onPress, variant = 'primary', icon, loading, disabled, accessibilityHint }: ButtonProps) {
  const { colors, radius } = useTheme();
  const palette = {
    primary: { bg: colors.accent, fg: colors.onAccent, border: colors.accent },
    secondary: { bg: colors.surfaceRaised, fg: colors.text, border: colors.border },
    danger: { bg: colors.dangerSoft, fg: colors.danger, border: colors.dangerSoft },
    ghost: { bg: 'transparent', fg: colors.text, border: 'transparent' },
  }[variant];
  const inactive = disabled || loading;

  return (
    <ScalePressable
      onPress={onPress}
      disabled={inactive}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: inactive, busy: loading }}
      style={[styles.base, { backgroundColor: palette.bg, borderColor: palette.border, borderRadius: radius.lg, opacity: disabled ? 0.45 : 1 }]}
    >
      {loading ? (
        <ActivityIndicator color={palette.fg} />
      ) : (
        <View style={styles.row}>
          {icon ? <Ionicons name={icon} size={22} color={palette.fg} /> : null}
          <AppText variant="bodyStrong" style={{ color: palette.fg }}>
            {label}
          </AppText>
        </View>
      )}
    </ScalePressable>
  );
}

const styles = StyleSheet.create({
  base: { minHeight: 60, paddingHorizontal: 24, justifyContent: 'center', alignItems: 'center', borderWidth: 1 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 10 },
});
