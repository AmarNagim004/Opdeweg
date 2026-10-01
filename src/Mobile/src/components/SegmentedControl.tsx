import { StyleSheet, View } from 'react-native';
import { useTheme } from '../theme/ThemeProvider';
import { AppText } from './AppText';
import { ScalePressable } from './Pressable';

interface SegmentedControlProps<T extends string> {
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
}

export function SegmentedControl<T extends string>({ value, options, onChange }: SegmentedControlProps<T>) {
  const { colors, radius } = useTheme();
  return (
    <View accessibilityRole="radiogroup" style={[styles.track, { backgroundColor: colors.surfaceRaised, borderRadius: radius.md }]}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <ScalePressable
            key={option.value}
            accessibilityRole="radio"
            accessibilityState={{ selected }}
            onPress={() => onChange(option.value)}
            style={[styles.segment, { borderRadius: radius.sm, backgroundColor: selected ? colors.accent : 'transparent' }]}
          >
            <AppText variant="label" style={{ color: selected ? colors.onAccent : colors.textMuted }}>
              {option.label}
            </AppText>
          </ScalePressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  track: { flexDirection: 'row', padding: 4, gap: 4 },
  segment: { flex: 1, minHeight: 44, alignItems: 'center', justifyContent: 'center' },
});
