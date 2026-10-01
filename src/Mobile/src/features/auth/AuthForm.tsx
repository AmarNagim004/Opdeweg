import { useRef, useState } from 'react';
import { KeyboardAvoidingView, Platform, StyleSheet, type TextInput, View } from 'react-native';
import { AppText } from '../../components/AppText';
import { Banner } from '../../components/Banner';
import { Button } from '../../components/Button';
import { Screen } from '../../components/Screen';
import { TextField } from '../../components/TextField';
import { ApiError } from '../../services/api/http';
import { authController } from './authController';

const friendlyError = (error: unknown): string => {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }

  switch (error.code) {
    case 'network_error':
      return 'Can’t reach Opdeweg. Check your connection.';
    case 'invalid_credentials':
      return 'E-mail or password is incorrect.';
    case 'email_taken':
      return 'An account with this e-mail already exists.';
    case 'rate_limited':
      return 'Too many attempts. Wait a minute and try again.';
    default:
      return error.message;
  }
};

export function AuthForm({ mode }: { mode: 'signIn' | 'signUp' }) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const emailRef = useRef<TextInput>(null);
  const passwordRef = useRef<TextInput>(null);

  const signUp = mode === 'signUp';
  const valid = email.includes('@') && password.length >= (signUp ? 8 : 1) && (!signUp || name.trim().length >= 2);

  const submit = async () => {
    if (!valid || busy) {
      return;
    }

    setBusy(true);
    setError(null);
    try {
      if (signUp) {
        await authController.signUp(name, email, password);
      } else {
        await authController.signIn(email, password);
      }
    } catch (e) {
      setError(friendlyError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Screen scroll edges={['bottom']} contentStyle={styles.content}>
        <AppText variant="title">{signUp ? 'Create your account' : 'Welcome back'}</AppText>
        <AppText tone="muted">{signUp ? 'Pick the name nearby drivers will see. You can hide it later.' : 'Sign in to continue driving.'}</AppText>

        {error ? <Banner tone="danger" icon="alert-circle-outline" title={error} /> : null}

        <View style={styles.fields}>
          {signUp ? (
            <TextField
              label="Display name"
              value={name}
              onChangeText={setName}
              autoCapitalize="words"
              autoComplete="nickname"
              maxLength={32}
              returnKeyType="next"
              onSubmitEditing={() => emailRef.current?.focus()}
            />
          ) : null}
          <TextField
            ref={emailRef}
            label="E-mail"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoComplete="email"
            textContentType="emailAddress"
            returnKeyType="next"
            onSubmitEditing={() => passwordRef.current?.focus()}
          />
          <TextField
            ref={passwordRef}
            label="Password"
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoComplete={signUp ? 'new-password' : 'current-password'}
            textContentType={signUp ? 'newPassword' : 'password'}
            returnKeyType="go"
            onSubmitEditing={() => void submit()}
          />
          {signUp ? (
            <AppText variant="caption" tone="muted">
              At least 8 characters.
            </AppText>
          ) : null}
        </View>

        <Button label={signUp ? 'Create account' : 'Sign in'} onPress={() => void submit()} loading={busy} disabled={!valid} />
      </Screen>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { paddingTop: 8, gap: 20 },
  fields: { gap: 16 },
});
