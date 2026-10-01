import { useEffect, useState } from 'react';
import { Animated, View } from 'react-native';

/** Small status light; pulses while something is live (e.g. voice connected, reconnecting). */
export function StatusDot({ color, size = 10, pulse = false }: { color: string; size?: number; pulse?: boolean }) {
  const [opacity] = useState(() => new Animated.Value(1));

  useEffect(() => {
    if (!pulse) {
      opacity.setValue(1);
      return;
    }

    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, { toValue: 0.35, duration: 700, useNativeDriver: true }),
        Animated.timing(opacity, { toValue: 1, duration: 700, useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [pulse, opacity]);

  return (
    <View style={{ width: size, height: size, alignItems: 'center', justifyContent: 'center' }}>
      <Animated.View style={{ width: size, height: size, borderRadius: size / 2, backgroundColor: color, opacity }} />
    </View>
  );
}
