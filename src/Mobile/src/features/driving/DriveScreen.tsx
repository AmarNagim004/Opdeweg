import { Alert, StyleSheet, View, useWindowDimensions } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { AppText } from '../../components/AppText';
import { Screen } from '../../components/Screen';
import { Wordmark } from '../../components/Wordmark';
import { useJourney } from '../../hooks/useJourney';
import { useNow } from '../../hooks/useNow';
import { useDrivingStore } from '../../store/drivingStore';
import { useVoiceStore } from '../../store/voiceStore';
import { useTheme } from '../../theme/ThemeProvider';
import { t } from '../../i18n/nl';
import { minutesSince } from '../../utils/format';
import { drivingController } from './drivingController';
import { ConnectionPill } from './components/ConnectionPill';
import { DriveButton } from './components/DriveButton';
import { StatusBanner } from './components/StatusBanner';
import { StatusTile } from './components/StatusTile';
import { VoiceControls } from './components/VoiceControls';
import type { RootStackParamList } from '../../navigation/types';

export function DriveScreen() {
  const { colors } = useTheme();
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const journey = useJourney();
  const session = useDrivingStore((s) => s.session);
  const inChannel = useVoiceStore((s) => s.participants.length);
  const muted = useVoiceStore((s) => s.muted);
  const now = useNow(30_000, journey.driving);
  const { height } = useWindowDimensions();
  // Fit the hero on common phones (incl. safe-area insets); the screen scrolls as a fallback on small ones.
  const buttonSize = Math.round(Math.min(216, Math.max(148, height * 0.22)));
  const compactControls = height < 880;

  const onDrivePress = async () => {
    if (journey.driving) {
      Alert.alert(t.drive.confirmStop.title, t.drive.confirmStop.body, [
        { text: t.drive.confirmStop.keepDriving, style: 'cancel' },
        { text: t.drive.confirmStop.stop, style: 'destructive', onPress: () => void drivingController.stop() },
      ]);
      return;
    }

    const result = await drivingController.start();
    if (result === 'needsPermissions') {
      navigation.navigate('Permissions');
    } else if (result === 'locationServicesOff') {
      Alert.alert(t.drive.startFailed.locationOffTitle, t.drive.startFailed.locationOffBody);
    } else if (result === 'offline') {
      Alert.alert(t.drive.startFailed.offlineTitle, t.drive.startFailed.offlineBody);
    } else if (result === 'error') {
      Alert.alert(t.drive.startFailed.errorTitle, t.drive.startFailed.errorBody);
    }
  };

  const voiceValue = t.voiceState[journey.voice];

  const voiceDot = journey.voice === 'live' ? colors.accent : journey.voice === 'off' ? colors.textFaint : colors.warning;

  return (
    <Screen scroll contentStyle={styles.content}>
      <View style={styles.header}>
        <Wordmark />
        <ConnectionPill />
      </View>

      <StatusBanner />

      <View style={styles.hero}>
        <DriveButton
          size={buttonSize}
          driving={journey.driving}
          busy={journey.transitioning}
          live={journey.driving}
          onPress={() => void onDrivePress()}
        />
        <AppText variant="title" align="center">
          {journey.driving ? t.drive.titleActive : t.drive.titleIdle}
        </AppText>
        <AppText tone="muted" align="center">
          {journey.driving ? t.drive.bodyActive(minutesSince(session?.startedAt, now)) : t.drive.bodyIdle}
        </AppText>
      </View>

      <View style={styles.tiles}>
        <StatusTile
          icon="people-outline"
          label={t.drive.nearbyLabel}
          value={journey.driving ? t.drive.nearbyValue(journey.nearby) : t.drive.nearbyNone}
          detail={journey.driving ? (journey.nearby > 0 ? t.drive.nearbyWithin : t.drive.nearbySearching) : t.drive.nearbyStartFirst}
          onPress={() => navigation.navigate('Main', { screen: 'Nearby' })}
        />
        <StatusTile
          icon="radio-outline"
          label={t.drive.voiceLabel}
          value={voiceValue}
          detail={journey.voice === 'live' ? t.drive.voiceInChannel(inChannel) : journey.driving ? t.drive.voiceAutomatic : undefined}
          dotColor={voiceDot}
          pulse={journey.voice === 'connecting' || journey.voice === 'reconnecting'}
          onPress={() => navigation.navigate('Main', { screen: 'Voice' })}
        />
      </View>

      {journey.driving ? (
        <>
          <VoiceControls compact={compactControls} />
          <View style={styles.privacy} accessibilityRole="text">
            <Ionicons name="location" size={14} color={colors.textMuted} />
            <AppText variant="caption" tone="muted">
              {t.drive.privacyLocation}
            </AppText>
            <AppText variant="caption" tone="faint">
              ·
            </AppText>
            <Ionicons name={muted ? 'mic-off' : 'mic'} size={14} color={journey.voice === 'live' && !muted ? colors.accentText : colors.textMuted} />
            <AppText variant="caption" tone={journey.voice === 'live' && !muted ? 'accent' : 'muted'}>
              {journey.voice === 'live' ? (muted ? t.drive.micMuted : t.drive.micLive) : t.drive.micIdle}
            </AppText>
          </View>
        </>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: 8 },
  content: { flexGrow: 1 },
  hero: { flexGrow: 1, alignItems: 'center', justifyContent: 'center', gap: 4, paddingBottom: 8 },
  tiles: { flexDirection: 'row', gap: 12 },
  privacy: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6 },
});
