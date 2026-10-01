import { Text, type TextProps, type TextStyle } from 'react-native';
import { useTheme } from '../theme/ThemeProvider';
import type { Theme } from '../theme/theme';

type Variant = keyof Theme['type'];
type Tone = 'default' | 'muted' | 'faint' | 'accent' | 'danger' | 'warning' | 'onAccent';

interface AppTextProps extends TextProps {
  variant?: Variant;
  tone?: Tone;
  align?: TextStyle['textAlign'];
  tabular?: boolean;
}

export function AppText({ variant = 'body', tone = 'default', align, tabular, style, ...rest }: AppTextProps) {
  const theme = useTheme();
  const color = {
    default: theme.colors.text,
    muted: theme.colors.textMuted,
    faint: theme.colors.textFaint,
    accent: theme.colors.accentText,
    danger: theme.colors.danger,
    warning: theme.colors.warning,
    onAccent: theme.colors.onAccent,
  }[tone];

  return (
    <Text
      maxFontSizeMultiplier={variant === 'display' || variant === 'title' ? 1.2 : 1.6}
      style={[theme.type[variant] as TextStyle, { color, textAlign: align }, tabular && { fontVariant: ['tabular-nums'] }, style]}
      {...rest}
    />
  );
}
