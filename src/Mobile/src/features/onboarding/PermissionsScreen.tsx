import { useState } from 'react';
import { Platform, StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useNavigation } from '@react-navigation/native';
import { AppText } from '../../components/AppText';
import { Button } from '../../components/Button';
import { Card } from '../../components/Card';
import { Screen } from '../../components/Screen';
import { usePermissions } from '../../hooks/usePermissions';
import {
  openAppSettings,
  requestAndroidExtras,
  requestBackgroundLocation,
  requestForegroundLocation,
  requestMicrophone,
  type PermissionStatus,
} from '../../services/location/permissions';
import { useUiStore } from '../../store/uiStore';
import { useTheme } from '../../theme/ThemeProvider';

interface Step {
  key: 'location' | 'microphone' | 'background';
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  body: string;
  status: PermissionStatus | undefined;
  optional?: boolean;
  request: () => Promise<unknown>;
}

/** Explains each permission in plain words before the OS prompt — and why it's safe to grant. */
export function PermissionsScreen() {
  const { colors } = useTheme();
  const navigation = useNavigation();
  const [permissions, refresh] = usePermissions();
  const [busy, setBusy] = useState(false);
  const setSeen = useUiStore((s) => s.setHasSeenPermissionPrimer);

  const steps: Step[] = [
    {
      key: 'location',
      icon: 'location',
      title: 'Location while driving',
      body: 'Finds drivers within 1 km. Only used during a drive; others see a rounded distance, never your position.',
      status: permissions?.location,
      request: requestForegroundLocation,
    },
    {
      key: 'microphone',
      icon: 'mic',
      title: 'Microphone',
      body: 'Live only when drivers are near you. Mute any time with one tap.',
      status: permissions?.microphone,
      request: async () => {
        await requestMicrophone();
        await requestAndroidExtras();
      },
    },
    {
      key: 'background',
      icon: 'moon',
      title: Platform.OS === 'ios' ? 'Keep working when locked' : 'Allow all the time',
      body: 'Recommended: keeps you connected with the screen off, even if the system restarts the app.',
      status: permissions?.backgroundLocation,
      optional: true,
      request: requestBackgroundLocation,
    },
  ];

  const next = steps.find((s) => s.status !== 'granted' && !(s.optional && s.status === 'denied'));
  const blocked = next?.status === 'denied';
  const requiredDone = steps.filter((s) => !s.optional).every((s) => s.status === 'granted');

  const onContinue = async () => {
    if (!next) {
      setSeen(true);
      navigation.goBack();
      return;
    }

    if (blocked) {
      await openAppSettings();
      return;
    }

    setBusy(true);
    try {
      await next.request();
    } finally {
      await refresh();
      setBusy(false);
    }
  };

  return (
    <Screen scroll edges={['top', 'bottom']}>
      <AppText variant="title" accessibilityRole="header" style={styles.title}>
        Set up for the road
      </AppText>
      <AppText tone="muted">Three quick permissions so Opdeweg can work hands-free.</AppText>

      {steps.map((step) => {
        const granted = step.status === 'granted';
        return (
          <Card key={step.key} style={[styles.step, next?.key === step.key && { borderColor: colors.accent, borderWidth: 1.5 }]}>
            <View style={[styles.icon, { backgroundColor: granted ? colors.accent : colors.surfaceRaised }]}>
              <Ionicons name={granted ? 'checkmark' : step.icon} size={22} color={granted ? colors.onAccent : colors.text} />
            </View>
            <View style={styles.stepText}>
              <AppText variant="bodyStrong">
                {step.title}
                {step.optional ? <AppText variant="caption" tone="muted">{'  '}optional</AppText> : null}
              </AppText>
              <AppText variant="caption" tone="muted">
                {step.body}
              </AppText>
            </View>
          </Card>
        );
      })}

      <View style={styles.actions}>
        <Button
          label={!next ? 'Done' : blocked ? 'Open Settings' : `Allow ${next.key === 'background' ? 'background' : next.key}`}
          icon={!next ? 'checkmark' : blocked ? 'settings-outline' : 'arrow-forward'}
          onPress={() => void onContinue()}
          loading={busy}
        />
        {next?.optional || (requiredDone && next) ? <Button label="Not now" variant="ghost" onPress={() => navigation.goBack()} /> : null}
        {!requiredDone ? <Button label="Later" variant="ghost" onPress={() => navigation.goBack()} /> : null}
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { marginTop: 16 },
  step: { flexDirection: 'row', gap: 16, alignItems: 'center', borderWidth: 1, borderColor: 'transparent' },
  icon: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center' },
  stepText: { flex: 1, gap: 4 },
  actions: { gap: 4, marginTop: 8 },
});
