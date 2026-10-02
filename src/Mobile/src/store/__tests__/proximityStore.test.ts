import { nearbyCount, useProximityStore } from '../proximityStore';
import type { ProximityGroup, ProximitySnapshot } from '../../types/api';

const voice = { url: 'wss://v', token: 't', room: 'opd-g1', identity: 'd1', expiresAt: '2030-01-01T00:00:00Z' };
const group = (id: string, withVoice = false): ProximityGroup => ({
  groupId: id,
  members: [{ id: 'd2', displayName: 'Sam', avatarUrl: null, approxDistanceMeters: 400, inVoiceGroup: true }],
  voice: withVoice ? voice : null,
});
const snapshot = (g: ProximityGroup | null): ProximitySnapshot => ({ sessionActive: true, group: g, nearby: [], nearbyCount: g ? 1 : 0, generatedAt: '' });

describe('proximity store', () => {
  beforeEach(() => useProximityStore.getState().reset());

  it('keeps the voice token across token-less snapshots of the same group', () => {
    useProximityStore.getState().applyGroupJoined(group('g1', true));
    useProximityStore.getState().applySnapshot(snapshot(group('g1')));
    expect(useProximityStore.getState().group?.voice).toEqual(voice);
  });

  it('drops the token when the server moves the driver to another group', () => {
    useProximityStore.getState().applyGroupJoined(group('g1', true));
    useProximityStore.getState().applySnapshot(snapshot(group('g2')));
    expect(useProximityStore.getState().group?.voice).toBeNull();
  });

  it('only leaves the group it was told about', () => {
    useProximityStore.getState().applyGroupJoined(group('g2'));
    useProximityStore.getState().applyGroupLeft('g1', 'outOfRange');
    expect(useProximityStore.getState().group?.groupId).toBe('g2');
    useProximityStore.getState().applyGroupLeft('g2', 'outOfRange');
    expect(useProximityStore.getState().group).toBeNull();
    expect(useProximityStore.getState().lastLeftReason).toBe('outOfRange');
  });

  it('counts group members and other nearby drivers', () => {
    useProximityStore.getState().applySnapshot({
      ...snapshot(group('g1')),
      nearby: [{ id: 'd3', displayName: 'Rijder', avatarUrl: null, approxDistanceMeters: 900, inVoiceGroup: false }],
    });
    expect(nearbyCount(useProximityStore.getState())).toBe(2);
  });
});
