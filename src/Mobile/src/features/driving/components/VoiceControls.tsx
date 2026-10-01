import { StyleSheet, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { AppText } from '../../../components/AppText';
import { ScalePressable } from '../../../components/Pressable';
import { setSpeakerOutput } from '../../../services/voice/audioSession';
import { useVoiceStore } from '../../../store/voiceStore';
import { useTheme } from '../../../theme/ThemeProvider';
import { voiceController } from '../../voice/voiceController';

/** Large, glanceable mute + output controls. Mute keeps the track published (no mic re-acquire). */
export function VoiceControls({ compact = false }: { compact?: boolean }) {
  const { colors } = useTheme();
  const muted = useVoiceStore((s) => s.muted);
  const output = useVoiceStore((s) => s.output);
  const setOutput = useVoiceStore((s) => s.setOutput);
  const size = compact ? 68 : 84;

  const toggleOutput = () => {
    const next = output === 'speaker' ? 'auto' : 'speaker';
    setOutput(next);
    void setSpeakerOutput(next === 'speaker');
  };

  return (
    <View style={styles.row}>
      <View style={styles.control}>
        <ScalePressable
          onPress={() => void voiceController.setMuted(!muted)}
          accessibilityRole="switch"
          accessibilityLabel="Microphone"
          accessibilityState={{ checked: !muted }}
          accessibilityHint={muted ? 'Unmutes your microphone' : 'Mutes your microphone'}
          style={[styles.circle, { width: size, height: size, borderRadius: size / 2, backgroundColor: muted ? colors.danger : colors.surfaceRaised, borderColor: muted ? colors.danger : colors.border }]}
        >
          <Ionicons name={muted ? 'mic-off' : 'mic'} size={size * 0.42} color={muted ? colors.onDanger : colors.text} />
        </ScalePressable>
        <AppText variant="label" tone={muted ? 'danger' : 'muted'}>
          {muted ? 'Muted' : 'Mic on'}
        </AppText>
      </View>

      <View style={styles.control}>
        <ScalePressable
          onPress={toggleOutput}
          accessibilityRole="switch"
          accessibilityLabel="Loudspeaker"
          accessibilityState={{ checked: output === 'speaker' }}
          accessibilityHint="Switches between the loudspeaker and automatic routing to Bluetooth or headphones"
          style={[styles.circle, { width: size, height: size, borderRadius: size / 2, backgroundColor: colors.surfaceRaised, borderColor: colors.border }]}
        >
          <Ionicons name={output === 'speaker' ? 'volume-high' : 'bluetooth'} size={size * 0.4} color={colors.text} />
        </ScalePressable>
        <AppText variant="label" tone="muted">
          {output === 'speaker' ? 'Speaker' : 'Auto audio'}
        </AppText>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', justifyContent: 'center', gap: 40 },
  control: { alignItems: 'center', gap: 8 },
  circle: { alignItems: 'center', justifyContent: 'center', borderWidth: 1 },
});
