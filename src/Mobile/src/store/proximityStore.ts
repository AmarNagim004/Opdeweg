import { create } from 'zustand';
import type { NearbyDriver, ProximityChangeReason, ProximityGroup, ProximitySnapshot, VoiceAccess } from '../types/api';

interface ProximityState {
  group: ProximityGroup | null;
  /** Drivers nearby but not in your voice group (not within range of every member). */
  nearby: NearbyDriver[];
  updatedAt: number | null;
  lastLeftReason: ProximityChangeReason | null;
  applySnapshot: (snapshot: ProximitySnapshot) => void;
  applyGroupJoined: (group: ProximityGroup) => void;
  applyRoster: (group: ProximityGroup) => void;
  applyGroupLeft: (groupId: string, reason: ProximityChangeReason) => void;
  setVoiceAccess: (groupId: string, voice: VoiceAccess) => void;
  reset: () => void;
}

/** The server is authoritative: the client only mirrors what it is told. */
export const useProximityStore = create<ProximityState>()((set, get) => ({
  group: null,
  nearby: [],
  updatedAt: null,
  lastLeftReason: null,

  applySnapshot: (snapshot) => {
    const current = get().group;
    let group = snapshot.group;
    // Snapshots from location responses carry no token; keep the one we already hold for the same group.
    if (group && !group.voice && current?.groupId === group.groupId) {
      group = { ...group, voice: current.voice };
    }

    set({ group, nearby: snapshot.nearby, updatedAt: Date.now() });
  },

  applyGroupJoined: (group) => set({ group, updatedAt: Date.now(), lastLeftReason: null }),

  applyRoster: (group) => {
    const current = get().group;
    if (current?.groupId !== group.groupId) {
      return;
    }

    set({ group: { ...group, voice: group.voice ?? current.voice }, updatedAt: Date.now() });
  },

  applyGroupLeft: (groupId, reason) => {
    if (get().group?.groupId === groupId) {
      set({ group: null, lastLeftReason: reason, updatedAt: Date.now() });
    }
  },

  setVoiceAccess: (groupId, voice) => {
    const current = get().group;
    if (current?.groupId === groupId) {
      set({ group: { ...current, voice } });
    }
  },

  reset: () => set({ group: null, nearby: [], updatedAt: null, lastLeftReason: null }),
}));

export function nearbyCount(state: Pick<ProximityState, 'group' | 'nearby'>): number {
  return (state.group?.members.length ?? 0) + state.nearby.length;
}
