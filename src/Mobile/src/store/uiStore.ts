import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { zustandStorage } from '../services/storage/kv';
import type { ClientConfig } from '../types/api';

export type ThemePreference = 'system' | 'dark' | 'light';

interface UiState {
  themePreference: ThemePreference;
  startMuted: boolean;
  keepScreenAwake: boolean;
  hasSeenPermissionPrimer: boolean;
  /** Last server-provided tuning (thresholds), so the background task has it offline too. */
  serverConfig: ClientConfig | null;
  setThemePreference: (preference: ThemePreference) => void;
  setStartMuted: (value: boolean) => void;
  setKeepScreenAwake: (value: boolean) => void;
  setHasSeenPermissionPrimer: (value: boolean) => void;
  setServerConfig: (config: ClientConfig) => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      themePreference: 'system',
      startMuted: false,
      keepScreenAwake: false,
      hasSeenPermissionPrimer: false,
      serverConfig: null,
      setThemePreference: (themePreference) => set({ themePreference }),
      setStartMuted: (startMuted) => set({ startMuted }),
      setKeepScreenAwake: (keepScreenAwake) => set({ keepScreenAwake }),
      setHasSeenPermissionPrimer: (hasSeenPermissionPrimer) => set({ hasSeenPermissionPrimer }),
      setServerConfig: (serverConfig) => set({ serverConfig }),
    }),
    { name: 'opdeweg.ui.v1', storage: zustandStorage },
  ),
);
