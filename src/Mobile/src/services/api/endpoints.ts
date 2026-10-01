import { request } from './http';
import type {
  AuthResponse,
  ClientConfig,
  DrivingSession,
  LocationUpdate,
  LocationUpdateResponse,
  Me,
  ProximitySnapshot,
  VoiceAccess,
} from '../../types/api';

export const authApi = {
  register: (email: string, password: string, displayName: string) =>
    request<AuthResponse>('/api/v1/auth/register', { method: 'POST', body: { email, password, displayName }, auth: false }),
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/v1/auth/login', { method: 'POST', body: { email, password }, auth: false }),
  logout: (refreshToken: string | null) => request<void>('/api/v1/auth/logout', { method: 'POST', body: { refreshToken } }),
};

export const meApi = {
  get: () => request<Me>('/api/v1/me'),
  update: (changes: Partial<Pick<Me, 'displayName' | 'avatarUrl' | 'shareDisplayName'>>) =>
    request<Me>('/api/v1/me', { method: 'PATCH', body: changes }),
  delete: () => request<void>('/api/v1/me', { method: 'DELETE' }),
};

export const drivingApi = {
  current: () => request<DrivingSession | undefined>('/api/v1/driving/sessions/current'),
  start: () => request<DrivingSession>('/api/v1/driving/sessions', { method: 'POST' }),
  end: () => request<void>('/api/v1/driving/sessions/current/end', { method: 'POST' }),
  postLocation: (update: LocationUpdate) =>
    request<LocationUpdateResponse>('/api/v1/driving/location', { method: 'POST', body: update }),
};

export const proximityApi = {
  snapshot: (includeVoice = false) => request<ProximitySnapshot>(`/api/v1/proximity?includeVoice=${includeVoice}`),
  voiceToken: () => request<VoiceAccess>('/api/v1/voice/token', { method: 'POST' }),
  config: () => request<ClientConfig>('/api/v1/config', { auth: false }),
};
