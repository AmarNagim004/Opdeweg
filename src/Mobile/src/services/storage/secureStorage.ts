import * as SecureStore from 'expo-secure-store';

/**
 * Auth tokens live in the Keychain / Android Keystore. AFTER_FIRST_UNLOCK lets the background
 * location task read them while the phone is locked in a car mount.
 */
const options: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK,
};

export const secureStorage = {
  get: (key: string) => SecureStore.getItemAsync(key, options),
  set: (key: string, value: string) => SecureStore.setItemAsync(key, value, options),
  remove: (key: string) => SecureStore.deleteItemAsync(key, options),
};
