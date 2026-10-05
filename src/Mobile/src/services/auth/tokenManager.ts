import { appConfig } from '../config';
import { secureStorage } from '../storage/secureStorage';
import type { AuthResponse } from '../../types/api';

const STORAGE_KEY = 'opdeweg.auth.v1';
const REFRESH_SKEW_MS = 60_000;

interface StoredTokens {
  accessToken: string;
  accessTokenExpiresAt: number;
  refreshToken: string;
}

type Listener = () => void;

/**
 * Owns the token pair. Works in both the UI and the headless background-location context
 * (lazily loads from secure storage), and de-duplicates concurrent refreshes.
 */
class TokenManager {
  private tokens: StoredTokens | null = null;
  private loaded = false;
  private refreshing: Promise<string | null> | null = null;
  private readonly signedOutListeners = new Set<Listener>();

  async load(): Promise<boolean> {
    if (!this.loaded) {
      const raw = await secureStorage.get(STORAGE_KEY);
      this.tokens = raw ? (JSON.parse(raw) as StoredTokens) : null;
      this.loaded = true;
    }

    return this.tokens != null;
  }

  async save(auth: AuthResponse): Promise<void> {
    this.tokens = {
      accessToken: auth.accessToken,
      accessTokenExpiresAt: Date.parse(auth.accessTokenExpiresAt),
      refreshToken: auth.refreshToken,
    };
    this.loaded = true;
    await secureStorage.set(STORAGE_KEY, JSON.stringify(this.tokens));
  }

  async clear(): Promise<void> {
    this.tokens = null;
    this.loaded = true;
    await secureStorage.remove(STORAGE_KEY);
  }

  get refreshToken(): string | null {
    return this.tokens?.refreshToken ?? null;
  }

  /** A non-expired access token, refreshing if needed. Null when signed out. */
  async getAccessToken(forceRefresh = false): Promise<string | null> {
    await this.load();
    if (!this.tokens) {
      return null;
    }

    if (!forceRefresh && this.tokens.accessTokenExpiresAt - Date.now() > REFRESH_SKEW_MS) {
      return this.tokens.accessToken;
    }

    this.refreshing ??= this.refresh().finally(() => {
      this.refreshing = null;
    });
    return this.refreshing;
  }

  onSignedOut(listener: Listener): () => void {
    this.signedOutListeners.add(listener);
    return () => this.signedOutListeners.delete(listener);
  }

  private async refresh(): Promise<string | null> {
    const refreshToken = this.tokens?.refreshToken;
    if (!refreshToken) {
      return null;
    }

    let response: Response;
    try {
      response = await fetch(`${appConfig.apiUrl}/api/v1/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
    } catch {
      // Offline: keep the (possibly expired) token; the request layer surfaces a network error.
      return this.tokens?.accessToken ?? null;
    }

    if (response.status === 401) {
      await this.clear();
      this.signedOutListeners.forEach((listener) => listener());
      return null;
    }

    if (!response.ok) {
      return this.tokens?.accessToken ?? null;
    }

    const auth = (await response.json()) as AuthResponse;
    await this.save(auth);
    return auth.accessToken;
  }
}

export const tokenManager = new TokenManager();
