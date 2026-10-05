import { create } from 'zustand';
import type { TransmitReason } from '../services/location/locationThrottle';

export type GpsQuality = 'unknown' | 'searching' | 'good' | 'weak' | 'unavailable';
export type PermissionLevel = 'unknown' | 'denied' | 'whileInUse' | 'always';

interface LocationState {
  gps: GpsQuality;
  accuracyMeters: number | null;
  lastFixAt: number | null;
  lastSentAt: number | null;
  lastSendReason: TransmitReason | null;
  permission: PermissionLevel;
  setFix: (accuracyMeters: number | null, at: number) => void;
  setGps: (gps: GpsQuality) => void;
  setSent: (reason: TransmitReason, at: number) => void;
  setPermission: (permission: PermissionLevel) => void;
  reset: () => void;
}

export const useLocationStore = create<LocationState>()((set) => ({
  gps: 'unknown',
  accuracyMeters: null,
  lastFixAt: null,
  lastSentAt: null,
  lastSendReason: null,
  permission: 'unknown',
  setFix: (accuracyMeters, at) =>
    set({ accuracyMeters, lastFixAt: at, gps: accuracyMeters != null && accuracyMeters > 75 ? 'weak' : 'good' }),
  setGps: (gps) => set({ gps }),
  setSent: (reason, at) => set({ lastSendReason: reason, lastSentAt: at }),
  setPermission: (permission) => set({ permission }),
  reset: () => set({ gps: 'unknown', accuracyMeters: null, lastFixAt: null, lastSentAt: null, lastSendReason: null }),
}));
