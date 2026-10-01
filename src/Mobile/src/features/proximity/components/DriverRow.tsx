import { StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { AppText } from '../../../components/AppText';
import { Avatar } from '../../../components/Avatar';
import { useTheme } from '../../../theme/ThemeProvider';
import { formatDistance } from '../../../utils/format';
import type { NearbyDriver } from '../../../types/api';

export function DriverRow({ driver, speaking }: { driver: NearbyDriver; speaking: boolean }) {
  const { colors } = useTheme();
  const distance = formatDistance(driver.approxDistanceMeters);

  return (
    <View
      style={styles.row}
      accessible
      accessibilityLabel={`${driver.displayName}, about ${distance} away${driver.inVoiceGroup ? ', in your voice channel' : ''}${speaking ? ', speaking' : ''}`}
    >
      <Avatar id={driver.id} name={driver.displayName} size={44} speaking={speaking} dimmed={!driver.inVoiceGroup} />
      <View style={styles.text}>
        <AppText variant="bodyStrong" numberOfLines={1}>
          {driver.displayName}
        </AppText>
        <AppText variant="caption" tone={speaking ? 'accent' : 'muted'}>
          {speaking ? 'Speaking' : driver.inVoiceGroup ? 'In your channel' : 'Nearby · other channel'}
        </AppText>
      </View>
      <View style={styles.distance}>
        {driver.inVoiceGroup ? <Ionicons name="radio" size={16} color={colors.accentText} /> : null}
        <AppText variant="label" tabular>
          {distance}
        </AppText>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 14, paddingVertical: 12, minHeight: 68 },
  text: { flex: 1, gap: 2 },
  distance: { flexDirection: 'row', alignItems: 'center', gap: 6 },
});
