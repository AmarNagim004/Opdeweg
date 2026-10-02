import { StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { AppText } from '../../components/AppText';
import { Button } from '../../components/Button';
import { PulseRings } from '../../components/PulseRings';
import { Screen } from '../../components/Screen';
import { Wordmark } from '../../components/Wordmark';
import { t } from '../../i18n/nl';
import { useTheme } from '../../theme/ThemeProvider';
import type { AuthStackParamList } from '../../navigation/types';

export function WelcomeScreen() {
  const { colors } = useTheme();
  const navigation = useNavigation<NativeStackNavigationProp<AuthStackParamList>>();

  return (
    <Screen edges={['top', 'bottom']}>
      <View style={styles.brand}>
        <Wordmark size={30} />
      </View>

      <View style={styles.hero}>
        <View style={styles.orb}>
          <PulseRings size={150} color={colors.accent} active />
          <View style={[styles.core, { backgroundColor: colors.accent }]}>
            <Ionicons name="navigate" size={56} color={colors.onAccent} />
          </View>
        </View>
        <AppText variant="display" align="center">
          {t.welcome.title}
        </AppText>
        <AppText variant="body" tone="muted" align="center" style={styles.tagline}>
          {t.welcome.body}
        </AppText>
      </View>

      <View style={styles.actions}>
        <Button label={t.welcome.getStarted} icon="arrow-forward" onPress={() => navigation.navigate('SignUp')} />
        <Button label={t.welcome.haveAccount} variant="ghost" onPress={() => navigation.navigate('SignIn')} />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  brand: { paddingTop: 16 },
  hero: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 16 },
  orb: { width: 260, height: 260, alignItems: 'center', justifyContent: 'center' },
  core: { width: 150, height: 150, borderRadius: 75, alignItems: 'center', justifyContent: 'center' },
  tagline: { maxWidth: 340 },
  actions: { gap: 4 },
});
