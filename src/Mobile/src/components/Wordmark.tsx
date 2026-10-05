import { View } from 'react-native';
import { useTheme } from '../theme/ThemeProvider';
import { AppText } from './AppText';

/** "opdeweg·" — lower-case wordmark with the signal dot. */
export function Wordmark({ size = 22 }: { size?: number }) {
  const { colors } = useTheme();
  return (
    <View accessibilityRole="header" accessibilityLabel="Opdeweg" style={{ flexDirection: 'row', alignItems: 'flex-end' }}>
      <AppText style={{ fontSize: size, lineHeight: size * 1.15, fontWeight: '800', letterSpacing: -0.04 * size }}>opdeweg</AppText>
      <View style={{ width: size * 0.3, height: size * 0.3, borderRadius: size, backgroundColor: colors.accent, marginLeft: 3, marginBottom: size * 0.16 }} />
    </View>
  );
}
