import { create } from 'zustand';
import type { DrivingSession, DrivingSessionEndReason } from '../types/api';

/** Offline → Online → Driving → (Nearby → Voice) — the latter two live in the proximity/voice stores. */
export type DrivingStatus = 'idle' | 'starting' | 'active' | 'stopping';

interface DrivingState {
  status: DrivingStatus;
  session: DrivingSession | null;
  lastEndReason: DrivingSessionEndReason | null;
  setStatus: (status: DrivingStatus) => void;
  setActive: (session: DrivingSession) => void;
  setEnded: (reason: DrivingSessionEndReason | null) => void;
}

export const useDrivingStore = create<DrivingState>()((set) => ({
  status: 'idle',
  session: null,
  lastEndReason: null,
  setStatus: (status) => set({ status }),
  setActive: (session) => set({ status: 'active', session, lastEndReason: null }),
  setEnded: (reason) => set({ status: 'idle', session: null, lastEndReason: reason }),
}));

export const isDriving = () => useDrivingStore.getState().status === 'active';
