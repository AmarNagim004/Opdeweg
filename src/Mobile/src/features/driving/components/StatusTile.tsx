import { StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { AppText } from '../../../components/AppText';
import { Card } from '../../../components/Card';
import { ScalePressable } from '../../../components/Pressable';
import { StatusDot } from '../../../components/StatusDot';
import { useTheme } from '../../../theme/ThemeProvider';

interface StatusTileProps {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  value: string;
  detail?: string;
  dotColor?: string;
  pulse?: boolean;
  onPress?: () => void;
}

export function StatusTile({ icon, label, value, detail, dotColor, pulse, onPress }: StatusTileProps) {
  const { colors } = useTheme();
  return (
    <ScalePressable
      containerStyle={styles.flex}
      style={styles.flex}
      onPress={onPress}
      disabled={!onPress}
      accessibilityRole={onPress ? 'button' : 'summary'}
      accessibilityLabel={`${label}: ${value}${detail ? `, ${detail}` : ''}`}
    >
      <Card style={styles.card}>
        <View style={styles.header}>
          <Ionicons name={icon} size={18} color={colors.textMuted} />
          <AppText variant="overline" tone="muted">
            {label}
          </AppText>
        </View>
        <View style={styles.valueRow}>
          {dotColor ? <StatusDot color={dotColor} size={12} pulse={pulse} /> : null}
          <AppText variant="headline" numberOfLines={1} adjustsFontSizeToFit tabular>
            {value}
          </AppText>
        </View>
        {detail ? (
          <AppText variant="caption" tone="muted" numberOfLines={1}>
            {detail}
          </AppText>
        ) : null}
      </Card>
    </ScalePressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  card: { flex: 1, gap: 10, minHeight: 112 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  valueRow: { flexDirection: 'row', alignItems: 'center', gap: 10 },
});
