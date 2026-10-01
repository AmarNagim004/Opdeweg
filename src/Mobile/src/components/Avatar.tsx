import { View } from 'react-native';
import { useTheme } from '../theme/ThemeProvider';
import { avatarHue, initials } from '../utils/format';
import { AppText } from './AppText';

interface AvatarProps {
  id: string;
  name: string;
  size?: number;
  speaking?: boolean;
  dimmed?: boolean;
}

/** Initials on a stable, anonymous colour. No photos of other drivers are needed or shown. */
export function Avatar({ id, name, size = 48, speaking = false, dimmed = false }: AvatarProps) {
  const { colors, scheme } = useTheme();
  const hue = avatarHue(id);
  const background = scheme === 'dark' ? `hsl(${hue}, 32%, 24%)` : `hsl(${hue}, 60%, 88%)`;
  const foreground = scheme === 'dark' ? `hsl(${hue}, 70%, 82%)` : `hsl(${hue}, 45%, 28%)`;
  const ring = Math.max(2, Math.round(size / 18));

  return (
    <View
      accessibilityLabel={speaking ? `${name}, speaking` : name}
      style={{
        width: size + ring * 2,
        height: size + ring * 2,
        borderRadius: (size + ring * 2) / 2,
        borderWidth: ring,
        borderColor: speaking ? colors.accent : 'transparent',
        alignItems: 'center',
        justifyContent: 'center',
        opacity: dimmed ? 0.55 : 1,
      }}
    >
      <View style={{ width: size, height: size, borderRadius: size / 2, backgroundColor: background, alignItems: 'center', justifyContent: 'center' }}>
        <AppText style={{ color: foreground, fontSize: size * 0.38, lineHeight: size * 0.46, fontWeight: '700' }}>{initials(name)}</AppText>
      </View>
    </View>
  );
}
