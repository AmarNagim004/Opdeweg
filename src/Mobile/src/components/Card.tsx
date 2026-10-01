import type { ReactNode } from 'react';
import { View, type StyleProp, type ViewStyle } from 'react-native';
import { useTheme } from '../theme/ThemeProvider';

export function Card({ children, style, padded = true }: { children: ReactNode; style?: StyleProp<ViewStyle>; padded?: boolean }) {
  const { colors, radius, scheme } = useTheme();
  return (
    <View
      style={[
        {
          backgroundColor: colors.surface,
          borderRadius: radius.lg,
          borderWidth: scheme === 'light' ? 1 : 0,
          borderColor: colors.border,
          padding: padded ? 20 : 0,
        },
        style,
      ]}
    >
      {children}
    </View>
  );
}
