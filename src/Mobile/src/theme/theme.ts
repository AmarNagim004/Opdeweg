import { palette } from './palette';

export type ColorScheme = 'light' | 'dark';

export interface ThemeColors {
  background: string;
  surface: string;
  surfaceRaised: string;
  surfacePressed: string;
  border: string;
  text: string;
  textMuted: string;
  textFaint: string;
  /** Signal lime: the brand colour, used for "on / live / driving". */
  accent: string;
  accentSoft: string;
  onAccent: string;
  /** Accent usable for text and icons on the background. */
  accentText: string;
  danger: string;
  dangerSoft: string;
  onDanger: string;
  warning: string;
  warningSoft: string;
  info: string;
  tabBar: string;
  shadow: string;
}

export interface Theme {
  scheme: ColorScheme;
  colors: ThemeColors;
  space: (steps: number) => number;
  radius: { sm: number; md: number; lg: number; xl: number; pill: number };
  type: typeof type;
}

const type = {
  display: { fontSize: 48, lineHeight: 52, fontWeight: '800', letterSpacing: -1.5 },
  title: { fontSize: 30, lineHeight: 36, fontWeight: '800', letterSpacing: -0.8 },
  headline: { fontSize: 20, lineHeight: 26, fontWeight: '700', letterSpacing: -0.3 },
  body: { fontSize: 17, lineHeight: 24, fontWeight: '400', letterSpacing: -0.1 },
  bodyStrong: { fontSize: 17, lineHeight: 24, fontWeight: '600', letterSpacing: -0.1 },
  label: { fontSize: 15, lineHeight: 20, fontWeight: '600', letterSpacing: 0 },
  caption: { fontSize: 13, lineHeight: 18, fontWeight: '500', letterSpacing: 0.1 },
  overline: { fontSize: 12, lineHeight: 16, fontWeight: '700', letterSpacing: 1.4, textTransform: 'uppercase' },
} as const;

const dark: ThemeColors = {
  background: palette.asphalt950,
  surface: palette.asphalt850,
  surfaceRaised: palette.asphalt800,
  surfacePressed: palette.asphalt700,
  border: palette.asphalt700,
  text: palette.fog100,
  textMuted: palette.fog400,
  textFaint: palette.fog500,
  accent: palette.signal,
  accentSoft: palette.signalSoft,
  onAccent: palette.asphalt950,
  accentText: palette.signal,
  danger: palette.red,
  dangerSoft: 'rgba(255, 91, 91, 0.16)',
  onDanger: palette.white,
  warning: palette.amber,
  warningSoft: 'rgba(255, 181, 71, 0.16)',
  info: palette.sky,
  tabBar: palette.asphalt900,
  shadow: '#000000',
};

const light: ThemeColors = {
  background: palette.paper,
  surface: palette.white,
  surfaceRaised: palette.white,
  surfacePressed: palette.fog100,
  border: palette.fog200,
  text: palette.asphalt950,
  textMuted: '#5A616B',
  textFaint: palette.fog500,
  accent: palette.signal,
  accentSoft: 'rgba(200, 240, 58, 0.35)',
  onAccent: palette.asphalt950,
  accentText: palette.signalInk,
  danger: palette.redInk,
  dangerSoft: 'rgba(196, 43, 43, 0.10)',
  onDanger: palette.white,
  warning: palette.amberInk,
  warningSoft: 'rgba(255, 181, 71, 0.22)',
  info: palette.skyInk,
  tabBar: palette.white,
  shadow: '#1B1F24',
};

export function createTheme(scheme: ColorScheme): Theme {
  return {
    scheme,
    colors: scheme === 'dark' ? dark : light,
    space: (steps) => steps * 4,
    radius: { sm: 10, md: 16, lg: 24, xl: 32, pill: 999 },
    type,
  };
}
