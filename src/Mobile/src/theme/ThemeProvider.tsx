import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useColorScheme } from 'react-native';
import { useUiStore } from '../store/uiStore';
import { createTheme, type Theme } from './theme';

const ThemeContext = createContext<Theme>(createTheme('dark'));

export function ThemeProvider({ children }: { children: ReactNode }) {
  const system = useColorScheme();
  const preference = useUiStore((s) => s.themePreference);
  const scheme = preference === 'system' ? (system === 'light' ? 'light' : 'dark') : preference;
  const theme = useMemo(() => createTheme(scheme), [scheme]);
  return <ThemeContext.Provider value={theme}>{children}</ThemeContext.Provider>;
}

export function useTheme(): Theme {
  return useContext(ThemeContext);
}
