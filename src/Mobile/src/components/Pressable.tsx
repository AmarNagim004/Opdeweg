import { useState, type ReactNode } from 'react';
import { Animated, Pressable, type PressableProps, type StyleProp, type ViewStyle } from 'react-native';
import * as Haptics from 'expo-haptics';

interface ScalePressableProps extends Omit<PressableProps, 'style' | 'children'> {
  /** Style of the animated content. */
  style?: StyleProp<ViewStyle>;
  /** Layout style of the outer touch target (e.g. `flex: 1` inside a row). */
  containerStyle?: StyleProp<ViewStyle>;
  children: ReactNode;
  haptic?: boolean;
  pressedScale?: number;
}

/** Pressable with a subtle spring scale and optional haptic tick: tactile without looking busy. */
export function ScalePressable({ style, containerStyle, children, haptic = true, pressedScale = 0.97, onPressIn, onPressOut, onPress, ...rest }: ScalePressableProps) {
  const [scale] = useState(() => new Animated.Value(1));
  const animate = (to: number) => Animated.spring(scale, { toValue: to, useNativeDriver: true, speed: 40, bounciness: 6 }).start();

  return (
    <Pressable
      style={containerStyle}
      hitSlop={8}
      onPressIn={(e) => {
        animate(pressedScale);
        onPressIn?.(e);
      }}
      onPressOut={(e) => {
        animate(1);
        onPressOut?.(e);
      }}
      onPress={(e) => {
        if (haptic) {
          void Haptics.selectionAsync();
        }

        onPress?.(e);
      }}
      {...rest}
    >
      <Animated.View style={[style, { transform: [{ scale }] }]}>{children}</Animated.View>
    </Pressable>
  );
}
