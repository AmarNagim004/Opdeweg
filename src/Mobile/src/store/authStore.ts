import { create } from 'zustand';
import type { Me } from '../types/api';

export type AuthStatus = 'restoring' | 'signedOut' | 'signedIn';

interface AuthState {
  status: AuthStatus;
  user: Me | null;
  setSignedIn: (user: Me) => void;
  setSignedOut: () => void;
  setUser: (user: Me) => void;
}

export const useAuthStore = create<AuthState>()((set) => ({
  status: 'restoring',
  user: null,
  setSignedIn: (user) => set({ status: 'signedIn', user }),
  setSignedOut: () => set({ status: 'signedOut', user: null }),
  setUser: (user) => set({ user }),
}));
