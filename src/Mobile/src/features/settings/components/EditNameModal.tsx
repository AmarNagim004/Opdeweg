import { useState } from 'react';
import { KeyboardAvoidingView, Modal, Platform, StyleSheet, View } from 'react-native';
import { AppText } from '../../../components/AppText';
import { Button } from '../../../components/Button';
import { TextField } from '../../../components/TextField';
import { t } from '../../../i18n/nl';
import { ApiError } from '../../../services/api/http';
import { useTheme } from '../../../theme/ThemeProvider';
import { authController } from '../../auth/authController';

export function EditNameModal({ visible, initial, onClose }: { visible: boolean; initial: string; onClose: () => void }) {
  const { colors, radius } = useTheme();
  const [name, setName] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await authController.updateProfile({ displayName: name });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t.editName.failed);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} onShow={() => setName(initial)}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.backdrop}>
        <View style={[styles.sheet, { backgroundColor: colors.surface, borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl }]}>
          <AppText variant="headline">{t.editName.title}</AppText>
          <AppText tone="muted">{t.editName.body}</AppText>
          <TextField label={t.editName.label} value={name} onChangeText={setName} autoFocus maxLength={32} error={error} returnKeyType="done" onSubmitEditing={() => void save()} />
          <Button label={t.editName.save} onPress={() => void save()} loading={saving} disabled={name.trim().length < 2} />
          <Button label={t.editName.cancel} variant="ghost" onPress={onClose} />
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0,0,0,0.5)' },
  sheet: { padding: 24, paddingBottom: 40, gap: 16 },
});
