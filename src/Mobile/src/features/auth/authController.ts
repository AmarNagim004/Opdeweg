import { authApi, meApi, proximityApi } from '../../services/api/endpoints';
import { ApiError } from '../../services/api/http';
import { tokenManager } from '../../services/auth/tokenManager';
import { proximityHub } from '../../services/signalr/proximityHub';
import { secureStorage } from '../../services/storage/secureStorage';
import { useAuthStore } from '../../store/authStore';
import { useUiStore } from '../../store/uiStore';
import { logger } from '../../utils/logger';
import { drivingController } from '../driving/drivingController';
import type { AuthResponse, Me } from '../../types/api';

const PROFILE_KEY = 'opdeweg.profile.v1';

async function completeSignIn(auth: AuthResponse): Promise<void> {
  await tokenManager.save(auth);
  await secureStorage.set(PROFILE_KEY, JSON.stringify(auth.user));
  useAuthStore.getState().setSignedIn(auth.user);
  await afterSignIn();
}

async function afterSignIn(): Promise<void> {
  void proximityApi
    .config()
    .then((config) => useUiStore.getState().setServerConfig(config))
    .catch(() => undefined);
  void proximityHub.start();
  await drivingController.resumeIfNeeded();
}

async function clearLocalSession(): Promise<void> {
  await drivingController.stop().catch(() => undefined);
  await proximityHub.stop();
  await tokenManager.clear();
  await secureStorage.remove(PROFILE_KEY);
  useAuthStore.getState().setSignedOut();
}

export const authController = {
  signIn: async (email: string, password: string) => completeSignIn(await authApi.login(email.trim(), password)),

  signUp: async (displayName: string, email: string, password: string) =>
    completeSignIn(await authApi.register(email.trim(), password, displayName.trim())),

  /** Restores a previous session at launch; works offline using the cached profile. */
  async restore(): Promise<void> {
    tokenManager.onSignedOut(() => void clearLocalSession());

    if (!(await tokenManager.load())) {
      useAuthStore.getState().setSignedOut();
      return;
    }

    const cached = await secureStorage.get(PROFILE_KEY);
    if (cached) {
      useAuthStore.getState().setSignedIn(JSON.parse(cached) as Me);
    }

    try {
      const me = await meApi.get();
      await secureStorage.set(PROFILE_KEY, JSON.stringify(me));
      useAuthStore.getState().setSignedIn(me);
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await clearLocalSession();
        return;
      }

      if (!cached) {
        // Offline with no cached profile: stay signed in with a minimal placeholder until we can reach the API.
        logger.warn('auth.restore_offline');
        useAuthStore.getState().setSignedIn({ id: '', email: '', displayName: 'Driver', avatarUrl: null, shareDisplayName: true, createdAt: '' });
      }
    }

    await afterSignIn();
  },

  async signOut(): Promise<void> {
    try {
      await authApi.logout(tokenManager.refreshToken);
    } catch {
      // Signing out locally must always work, even offline.
    }

    await clearLocalSession();
  },

  async updateProfile(changes: Partial<Pick<Me, 'displayName' | 'shareDisplayName'>>): Promise<void> {
    const me = await meApi.update(changes);
    await secureStorage.set(PROFILE_KEY, JSON.stringify(me));
    useAuthStore.getState().setUser(me);
  },

  async deleteAccount(): Promise<void> {
    await meApi.delete();
    await clearLocalSession();
  },
};
