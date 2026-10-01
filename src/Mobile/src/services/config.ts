import Constants from 'expo-constants';

function resolveApiUrl(): string {
  const fromEnv = process.env.EXPO_PUBLIC_API_URL;
  const fromConfig = (Constants.expoConfig?.extra as { apiUrl?: string } | undefined)?.apiUrl;
  const url = (fromEnv || fromConfig || 'http://localhost:5080').replace(/\/+$/, '');

  // Release builds must talk to the API over TLS; plain HTTP is only for local development.
  if (!__DEV__ && !url.startsWith('https://')) {
    throw new Error(`Refusing insecure API URL in a release build: ${url}`);
  }

  return url;
}

export const appConfig = {
  apiUrl: resolveApiUrl(),
  get hubUrl() {
    return `${this.apiUrl}/hubs/proximity`;
  },
  requestTimeoutMs: 15_000,
} as const;
