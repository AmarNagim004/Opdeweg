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
import { t } from '../../i18n/nl';
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

const allowLabel: Record<Step['key'], string> = {
  location: t.permissions.allowLocation,
  microphone: t.permissions.allowMicrophone,
  background: t.permissions.allowBackground,
};

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
      title: t.permissions.locationTitle,
      body: t.permissions.locationBody,
      status: permissions?.location,
      request: requestForegroundLocation,
    },
    {
      key: 'microphone',
      icon: 'mic',
      title: t.permissions.micTitle,
      body: t.permissions.micBody,
      status: permissions?.microphone,
      request: async () => {
        await requestMicrophone();
        await requestAndroidExtras();
      },
    },
    {
      key: 'background',
      icon: 'moon',
      title: Platform.OS === 'ios' ? t.permissions.backgroundTitleIos : t.permissions.backgroundTitleAndroid,
      body: t.permissions.backgroundBody,
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
        {t.permissions.title}
      </AppText>
      <AppText tone="muted">{t.permissions.body}</AppText>

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
                {step.optional ? <AppText variant="caption" tone="muted">{'  '}{t.permissions.optional}</AppText> : null}
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
          label={!next ? t.permissions.done : blocked ? t.permissions.openSettings : allowLabel[next.key]}
          icon={!next ? 'checkmark' : blocked ? 'settings-outline' : 'arrow-forward'}
          onPress={() => void onContinue()}
          loading={busy}
        />
        {next?.optional || (requiredDone && next) ? <Button label={t.permissions.notNow} variant="ghost" onPress={() => navigation.goBack()} /> : null}
        {!requiredDone ? <Button label={t.permissions.later} variant="ghost" onPress={() => navigation.goBack()} /> : null}
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
