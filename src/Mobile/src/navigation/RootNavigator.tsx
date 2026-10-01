import { ActivityIndicator, View } from 'react-native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { WelcomeScreen } from '../features/auth/WelcomeScreen';
import { SignInScreen } from '../features/auth/SignInScreen';
import { SignUpScreen } from '../features/auth/SignUpScreen';
import { PermissionsScreen } from '../features/onboarding/PermissionsScreen';
import { Wordmark } from '../components/Wordmark';
import { useAuthStore } from '../store/authStore';
import { useTheme } from '../theme/ThemeProvider';
import { MainTabs } from './MainTabs';
import type { AuthStackParamList, RootStackParamList } from './types';

const Root = createNativeStackNavigator<RootStackParamList>();
const AuthStack = createNativeStackNavigator<AuthStackParamList>();

function AuthNavigator() {
  const { colors } = useTheme();
  return (
    <AuthStack.Navigator
      screenOptions={{
        headerShadowVisible: false,
        headerTitle: '',
        headerStyle: { backgroundColor: colors.background },
        headerTintColor: colors.text,
        contentStyle: { backgroundColor: colors.background },
      }}
    >
      <AuthStack.Screen name="Welcome" component={WelcomeScreen} options={{ headerShown: false }} />
      <AuthStack.Screen name="SignIn" component={SignInScreen} />
      <AuthStack.Screen name="SignUp" component={SignUpScreen} />
    </AuthStack.Navigator>
  );
}

function Splash() {
  const { colors } = useTheme();
  return (
    <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', gap: 24, backgroundColor: colors.background }}>
      <Wordmark size={36} />
      <ActivityIndicator color={colors.accent} />
    </View>
  );
}

export function RootNavigator() {
  const { colors } = useTheme();
  const status = useAuthStore((s) => s.status);

  if (status === 'restoring') {
    return <Splash />;
  }

  return (
    <Root.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.background } }}>
      {status === 'signedIn' ? (
        <>
          <Root.Screen name="Main" component={MainTabs} />
          <Root.Screen name="Permissions" component={PermissionsScreen} options={{ presentation: 'modal' }} />
        </>
      ) : (
        <Root.Screen name="Auth" component={AuthNavigator} />
      )}
    </Root.Navigator>
  );
}
