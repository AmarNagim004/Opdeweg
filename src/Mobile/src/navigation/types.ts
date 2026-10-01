import type { NavigatorScreenParams } from '@react-navigation/native';

export type AuthStackParamList = {
  Welcome: undefined;
  SignIn: undefined;
  SignUp: undefined;
};

export type MainTabParamList = {
  Drive: undefined;
  Nearby: undefined;
  Voice: undefined;
  Settings: undefined;
};

export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList>;
  Main: NavigatorScreenParams<MainTabParamList>;
  Permissions: undefined;
};
