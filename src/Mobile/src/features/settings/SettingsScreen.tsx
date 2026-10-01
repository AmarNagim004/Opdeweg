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
import { formatDistance } from '../../utils/format';
import { authController } from '../auth/authController';
import { EditNameModal } from './components/EditNameModal';
import { SettingsRow } from './components/SettingsRow';
import type { RootStackParamList } from '../../navigation/types';

const permissionLabel = (status: PermissionStatus | undefined): [string, 'accent' | 'danger' | 'muted'] =>
  status === 'granted' ? ['Allowed', 'accent'] : status === 'denied' ? ['Denied', 'danger'] : status === 'notApplicable' ? ['Not needed', 'muted'] : ['Not set', 'muted'];

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
      Alert.alert('Couldn’t update privacy setting', 'Please try again when you’re online.');
    }
  };

  const confirmSignOut = () =>
    Alert.alert('Sign out?', 'Any active drive will end.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Sign out', style: 'destructive', onPress: () => void authController.signOut() },
    ]);

  const confirmDelete = () =>
    Alert.alert('Delete account?', 'This permanently deletes your account and all associated data. This cannot be undone.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: () => void authController.deleteAccount().catch(() => Alert.alert('Couldn’t delete your account', 'Please try again when you’re online.')),
      },
    ]);

  const [loc, locTone] = permissionLabel(permissions?.location);
  const [bg, bgTone] = permissionLabel(permissions?.backgroundLocation);
  const [mic, micTone] = permissionLabel(permissions?.microphone);
  const [bt, btTone] = permissionLabel(permissions?.bluetooth);

  return (
    <Screen scroll>
      <AppText variant="title" accessibilityRole="header" style={styles.title}>
        Settings
      </AppText>

      <Card style={styles.profile}>
        <Avatar id={user?.id ?? 'me'} name={user?.displayName ?? 'Driver'} size={56} />
        <View style={styles.profileText}>
          <AppText variant="headline" numberOfLines={1}>
            {user?.displayName}
          </AppText>
          <AppText variant="caption" tone="muted" numberOfLines={1}>
            {user?.email}
          </AppText>
        </View>
      </Card>

      <Section title="Account">
        <SettingsRow first icon="person-outline" label="Display name" value={user?.displayName} onPress={() => setEditing(true)} />
        <SettingsRow icon="log-out-outline" label="Sign out" onPress={confirmSignOut} />
      </Section>

      <Section title="Privacy">
        <SettingsRow first icon="eye-outline" label="Show my name to nearby drivers" toggle={{ value: user?.shareDisplayName ?? true, onChange: (v) => void toggleShareName(v) }}>
          <AppText variant="caption" tone="muted">
            When off, others see “Driver”.
          </AppText>
        </SettingsRow>
        <SettingsRow icon="shield-checkmark-outline" label="How your location is used">
          <AppText variant="caption" tone="muted">
            Shared only during a drive, never as exact coordinates. Others see a rounded distance. No route history is stored.
          </AppText>
        </SettingsRow>
      </Section>

      <Section title="Permissions">
        <SettingsRow first icon="location-outline" label="Location" value={loc} valueTone={locTone} onPress={() => navigation.navigate('Permissions')} />
        <SettingsRow icon="moon-outline" label="Background location" value={bg} valueTone={bgTone} onPress={() => void openAppSettings()} />
        <SettingsRow icon="mic-outline" label="Microphone" value={mic} valueTone={micTone} onPress={() => navigation.navigate('Permissions')} />
        {permissions?.bluetooth !== 'notApplicable' ? (
          <SettingsRow icon="bluetooth-outline" label="Nearby devices (Bluetooth)" value={bt} valueTone={btTone} onPress={() => void openAppSettings()} />
        ) : null}
      </Section>

      <Section title="Audio">
        <SettingsRow
          first
          icon="volume-high-outline"
          label="Use loudspeaker"
          toggle={{
            value: output === 'speaker',
            onChange: (speaker) => {
              setOutput(speaker ? 'speaker' : 'auto');
              void setSpeakerOutput(speaker);
            },
          }}
        >
          <AppText variant="caption" tone="muted">
            Off: car Bluetooth, intercoms and headphones are used automatically.
          </AppText>
        </SettingsRow>
        {supportsRoutePicker ? <SettingsRow icon="headset-outline" label="Choose audio device" onPress={() => void showAudioRoutePicker()} /> : null}
      </Section>

      <Section title="Voice">
        <SettingsRow first icon="mic-off-outline" label="Start drives muted" toggle={{ value: ui.startMuted, onChange: ui.setStartMuted }} />
        <SettingsRow icon="phone-portrait-outline" label="Keep screen on while driving" toggle={{ value: ui.keepScreenAwake, onChange: ui.setKeepScreenAwake }} />
      </Section>

      <Section title="Location & distance">
        <SettingsRow first icon="git-network-outline" label="Voice range">
          <AppText variant="caption" tone="muted">
            Join within {formatDistance(join)} · leave beyond {formatDistance(leave)}. The gap prevents flickering at the edge.
          </AppText>
        </SettingsRow>
        <SettingsRow icon="speedometer-outline" label="Update frequency">
          <AppText variant="caption" tone="muted">
            Every {step} m moved or {interval} s — never continuously.
          </AppText>
        </SettingsRow>
      </Section>

      <Section title="Appearance">
        <View style={styles.segment}>
          <SegmentedControl<ThemePreference>
            value={ui.themePreference}
            onChange={ui.setThemePreference}
            options={[
              { value: 'system', label: 'System' },
              { value: 'dark', label: 'Dark' },
              { value: 'light', label: 'Light' },
            ]}
          />
        </View>
      </Section>

      <Section title="About">
        <SettingsRow first icon="information-circle-outline" label="Opdeweg" value={`v${Constants.expoConfig?.version ?? '0.0.0'}`} />
        <SettingsRow icon="trash-outline" label="Delete account" destructive onPress={confirmDelete} />
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
