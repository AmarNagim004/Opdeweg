import { useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';
import Constants from 'expo-constants';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { AppText } from '../../components/AppText';
import { Avatar } from '../../components/Avatar';
import { Card } from '../../components/Card';
import { Screen } from '../../components/Screen';
import { SegmentedControl } from '../../components/SegmentedControl';
import { usePermissions } from '../../hooks/usePermissions';
import { openAppSettings, type PermissionStatus } from '../../services/location/permissions';
import { setSpeakerOutput, showAudioRoutePicker, supportsRoutePicker } from '../../services/voice/audioSession';
import { useAuthStore } from '../../store/authStore';
import { useUiStore, type ThemePreference } from '../../store/uiStore';
import { useVoiceStore } from '../../store/voiceStore';
import { t } from '../../i18n/nl';
import { authController } from '../auth/authController';
import { EditNameModal } from './components/EditNameModal';
import { SettingsRow } from './components/SettingsRow';
import type { RootStackParamList } from '../../navigation/types';

const permissionLabel = (status: PermissionStatus | undefined): [string, 'accent' | 'danger' | 'muted'] =>
  status === 'granted'
    ? [t.settings.permissionGranted, 'accent']
    : status === 'denied'
      ? [t.settings.permissionDenied, 'danger']
      : status === 'notApplicable'
        ? [t.settings.permissionNotNeeded, 'muted']
        : [t.settings.permissionNotSet, 'muted'];

export function SettingsScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const user = useAuthStore((s) => s.user);
  const ui = useUiStore();
  const output = useVoiceStore((s) => s.output);
  const setOutput = useVoiceStore((s) => s.setOutput);
  const [permissions] = usePermissions();
  const [editing, setEditing] = useState(false);

  const join = ui.serverConfig?.proximity.joinDistanceMeters ?? 1000;
  const leave = ui.serverConfig?.proximity.leaveDistanceMeters ?? 1100;
  const step = ui.serverConfig?.location.distanceThresholdMeters ?? 50;
  const interval = ui.serverConfig?.location.maxIntervalSeconds ?? 15;

  const toggleShareName = async (share: boolean) => {
    try {
      await authController.updateProfile({ shareDisplayName: share });
    } catch {
      Alert.alert(t.settings.updateFailed.title, t.settings.updateFailed.body);
    }
  };

  const confirmSignOut = () =>
    Alert.alert(t.settings.confirmSignOut.title, t.settings.confirmSignOut.body, [
      { text: t.settings.confirmSignOut.cancel, style: 'cancel' },
      { text: t.settings.confirmSignOut.confirm, style: 'destructive', onPress: () => void authController.signOut() },
    ]);

  const confirmDelete = () =>
    Alert.alert(t.settings.confirmDelete.title, t.settings.confirmDelete.body, [
      { text: t.settings.confirmDelete.cancel, style: 'cancel' },
      {
        text: t.settings.confirmDelete.confirm,
        style: 'destructive',
        onPress: () => void authController.deleteAccount().catch(() => Alert.alert(t.settings.deleteFailed.title, t.settings.deleteFailed.body)),
      },
    ]);

  const [loc, locTone] = permissionLabel(permissions?.location);
  const [bg, bgTone] = permissionLabel(permissions?.backgroundLocation);
  const [mic, micTone] = permissionLabel(permissions?.microphone);
  const [bt, btTone] = permissionLabel(permissions?.bluetooth);

  return (
    <Screen scroll>
      <AppText variant="title" accessibilityRole="header" style={styles.title}>
        {t.settings.title}
      </AppText>

      <Card style={styles.profile}>
        <Avatar id={user?.id ?? 'me'} name={user?.displayName ?? t.anonymousName} size={56} />
        <View style={styles.profileText}>
          <AppText variant="headline" numberOfLines={1}>
            {user?.displayName}
          </AppText>
          <AppText variant="caption" tone="muted" numberOfLines={1}>
            {user?.email}
          </AppText>
        </View>
      </Card>

      <Section title={t.settings.account}>
        <SettingsRow first icon="person-outline" label={t.settings.displayName} value={user?.displayName} onPress={() => setEditing(true)} />
        <SettingsRow icon="log-out-outline" label={t.settings.signOut} onPress={confirmSignOut} />
      </Section>

      <Section title={t.settings.privacy}>
        <SettingsRow first icon="eye-outline" label={t.settings.shareName} toggle={{ value: user?.shareDisplayName ?? true, onChange: (v) => void toggleShareName(v) }}>
          <AppText variant="caption" tone="muted">
            {t.settings.shareNameHint}
          </AppText>
        </SettingsRow>
        <SettingsRow icon="shield-checkmark-outline" label={t.settings.locationUse}>
          <AppText variant="caption" tone="muted">
            {t.settings.locationUseBody}
          </AppText>
        </SettingsRow>
      </Section>

      <Section title={t.settings.permissions}>
        <SettingsRow first icon="location-outline" label={t.settings.location} value={loc} valueTone={locTone} onPress={() => navigation.navigate('Permissions')} />
        <SettingsRow icon="moon-outline" label={t.settings.backgroundLocation} value={bg} valueTone={bgTone} onPress={() => void openAppSettings()} />
        <SettingsRow icon="mic-outline" label={t.settings.microphone} value={mic} valueTone={micTone} onPress={() => navigation.navigate('Permissions')} />
        {permissions?.bluetooth !== 'notApplicable' ? (
          <SettingsRow icon="bluetooth-outline" label={t.settings.bluetooth} value={bt} valueTone={btTone} onPress={() => void openAppSettings()} />
        ) : null}
      </Section>

      <Section title={t.settings.audio}>
        <SettingsRow
          first
          icon="volume-high-outline"
          label={t.settings.useSpeaker}
          toggle={{
            value: output === 'speaker',
            onChange: (speaker) => {
              setOutput(speaker ? 'speaker' : 'auto');
              void setSpeakerOutput(speaker);
            },
          }}
        >
          <AppText variant="caption" tone="muted">
            {t.settings.useSpeakerHint}
          </AppText>
        </SettingsRow>
        {supportsRoutePicker ? <SettingsRow icon="headset-outline" label={t.settings.chooseAudioDevice} onPress={() => void showAudioRoutePicker()} /> : null}
      </Section>

      <Section title={t.settings.voice}>
        <SettingsRow first icon="mic-off-outline" label={t.settings.startMuted} toggle={{ value: ui.startMuted, onChange: ui.setStartMuted }} />
        <SettingsRow icon="phone-portrait-outline" label={t.settings.keepAwake} toggle={{ value: ui.keepScreenAwake, onChange: ui.setKeepScreenAwake }} />
      </Section>

      <Section title={t.settings.locationAndRange}>
        <SettingsRow first icon="git-network-outline" label={t.settings.range}>
          <AppText variant="caption" tone="muted">
            {t.settings.rangeBody(join, leave)}
          </AppText>
        </SettingsRow>
        <SettingsRow icon="speedometer-outline" label={t.settings.updateFrequency}>
          <AppText variant="caption" tone="muted">
            {t.settings.updateFrequencyBody(step, interval)}
          </AppText>
        </SettingsRow>
      </Section>

      <Section title={t.settings.appearance}>
        <View style={styles.segment}>
          <SegmentedControl<ThemePreference>
            value={ui.themePreference}
            onChange={ui.setThemePreference}
            options={[
              { value: 'system', label: t.settings.system },
              { value: 'dark', label: t.settings.dark },
              { value: 'light', label: t.settings.light },
            ]}
          />
        </View>
      </Section>

      <Section title={t.settings.about}>
        <SettingsRow first icon="information-circle-outline" label="Opdeweg" value={`v${Constants.expoConfig?.version ?? '0.0.0'}`} />
        <SettingsRow icon="trash-outline" label={t.settings.deleteAccount} destructive onPress={confirmDelete} />
      </Section>

      <EditNameModal visible={editing} initial={user?.displayName ?? ''} onClose={() => setEditing(false)} />
    </Screen>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View style={styles.section}>
      <AppText variant="overline" tone="muted" style={styles.sectionTitle}>
        {title}
      </AppText>
      <Card padded={false}>{children}</Card>
    </View>
  );
}

const styles = StyleSheet.create({
  title: { marginTop: 12 },
  profile: { flexDirection: 'row', alignItems: 'center', gap: 16 },
  profileText: { flex: 1, gap: 2 },
  section: { gap: 8 },
  sectionTitle: { paddingHorizontal: 4 },
  segment: { padding: 12 },
});
