import { useEffect, useState } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';

/** Concentric rings expanding outward — the "live" signal around the drive button and active speakers. */
export function PulseRings({ size, color, active, rings = 3, spread = 1.55 }: { size: number; color: string; active: boolean; rings?: number; spread?: number }) {
  const [values] = useState(() => Array.from({ length: rings }, () => new Animated.Value(0)));

  useEffect(() => {
    if (!active) {
      values.forEach((v) => v.setValue(0));
      return;
    }

    const animations = values.map((value, index) =>
      Animated.loop(
        Animated.sequence([
          Animated.delay((index * 2400) / values.length),
          Animated.timing(value, { toValue: 1, duration: 2400, easing: Easing.out(Easing.cubic), useNativeDriver: true }),
          Animated.timing(value, { toValue: 0, duration: 0, useNativeDriver: true }),
        ]),
      ),
    );
    animations.forEach((a) => a.start());
    return () => animations.forEach((a) => a.stop());
  }, [active, values]);

  return (
    <View pointerEvents="none" style={[StyleSheet.absoluteFill, styles.center]}>
      {values.map((value, index) => (
        <Animated.View
          key={index}
          style={{
            position: 'absolute',
            width: size,
            height: size,
            borderRadius: size / 2,
            borderWidth: 2,
            borderColor: color,
            opacity: value.interpolate({ inputRange: [0, 0.1, 1], outputRange: [0, 0.55, 0] }),
            transform: [{ scale: value.interpolate({ inputRange: [0, 1], outputRange: [1, spread] }) }],
          }}
        />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({ center: { alignItems: 'center', justifyContent: 'center' } });
