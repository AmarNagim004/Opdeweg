import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import Ionicons from '@expo/vector-icons/Ionicons';
import { DriveScreen } from '../features/driving/DriveScreen';
import { NearbyScreen } from '../features/proximity/NearbyScreen';
import { SettingsScreen } from '../features/settings/SettingsScreen';
import { VoiceScreen } from '../features/voice/VoiceScreen';
import { useJourney } from '../hooks/useJourney';
import { t } from '../i18n/nl';
import { useTheme } from '../theme/ThemeProvider';
import type { MainTabParamList } from './types';

const Tab = createBottomTabNavigator<MainTabParamList>();

const icons: Record<keyof MainTabParamList, [keyof typeof Ionicons.glyphMap, keyof typeof Ionicons.glyphMap]> = {
  Drive: ['navigate', 'navigate-outline'],
  Nearby: ['people', 'people-outline'],
  Voice: ['radio', 'radio-outline'],
  Settings: ['settings', 'settings-outline'],
};

const labels: Record<keyof MainTabParamList, string> = {
  Drive: t.tabs.drive,
  Nearby: t.tabs.nearby,
  Voice: t.tabs.voice,
  Settings: t.tabs.settings,
};

export function MainTabs() {
  const { colors } = useTheme();
  const journey = useJourney();

  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        title: labels[route.name],
        tabBarActiveTintColor: colors.accentText,
        tabBarInactiveTintColor: colors.textFaint,
        tabBarStyle: { backgroundColor: colors.tabBar, borderTopColor: colors.border, height: 88, paddingTop: 10 },
        tabBarLabelStyle: { fontSize: 12, fontWeight: '600' },
        tabBarIcon: ({ focused, color }) => <Ionicons name={icons[route.name][focused ? 0 : 1]} size={28} color={color} />,
      })}
    >
      <Tab.Screen name="Drive" component={DriveScreen} />
      <Tab.Screen
        name="Nearby"
        component={NearbyScreen}
        options={{ tabBarBadge: journey.driving && journey.nearby > 0 ? journey.nearby : undefined, tabBarBadgeStyle: { backgroundColor: colors.accent, color: colors.onAccent } }}
      />
      <Tab.Screen name="Voice" component={VoiceScreen} />
      <Tab.Screen name="Settings" component={SettingsScreen} />
    </Tab.Navigator>
  );
}
