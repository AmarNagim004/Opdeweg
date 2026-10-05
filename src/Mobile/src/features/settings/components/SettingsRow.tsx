import type { ReactNode } from 'react';
import { StyleSheet, Switch, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { AppText } from '../../../components/AppText';
import { ScalePressable } from '../../../components/Pressable';
import { useTheme } from '../../../theme/ThemeProvider';

interface SettingsRowProps {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  value?: string;
  valueTone?: 'muted' | 'accent' | 'danger' | 'warning';
  onPress?: () => void;
  toggle?: { value: boolean; onChange: (value: boolean) => void };
  destructive?: boolean;
  first?: boolean;
  children?: ReactNode;
}

export function SettingsRow({ icon, label, value, valueTone = 'muted', onPress, toggle, destructive, first, children }: SettingsRowProps) {
  const { colors } = useTheme();
  const tint = destructive ? colors.danger : colors.text;

  const content = (
    <View style={[styles.row, !first && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
      <Ionicons name={icon} size={22} color={destructive ? colors.danger : colors.textMuted} />
      <View style={styles.text}>
        <AppText variant="bodyStrong" style={{ color: tint }}>
          {label}
        </AppText>
        {children}
      </View>
      {value ? (
        <AppText variant="label" tone={valueTone} numberOfLines={1} style={styles.value}>
          {value}
        </AppText>
      ) : null}
      {toggle ? (
        <Switch
          value={toggle.value}
          onValueChange={toggle.onChange}
          trackColor={{ true: colors.accent, false: colors.surfacePressed }}
          thumbColor={colors.surfaceRaised}
          ios_backgroundColor={colors.surfacePressed}
          accessibilityLabel={label}
        />
      ) : onPress ? (
        <Ionicons name="chevron-forward" size={18} color={colors.textFaint} />
      ) : null}
    </View>
  );

  return onPress ? (
    <ScalePressable onPress={onPress} pressedScale={0.99} accessibilityRole="button" accessibilityLabel={value ? `${label}, ${value}` : label}>
      {content}
    </ScalePressable>
  ) : (
    content
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 14, minHeight: 60, paddingVertical: 12, paddingHorizontal: 16 },
  text: { flex: 1, gap: 2 },
  value: { maxWidth: '45%' },
});
