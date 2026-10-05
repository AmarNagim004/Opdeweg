/* eslint-disable import/first -- jest.mock() calls must precede the imports they affect. */
import type { VoiceAccess } from '../../../types/api';

const mockConnect = jest.fn<Promise<void>, [VoiceAccess, boolean]>();
const mockDisconnect = jest.fn<Promise<void>, []>();
const mockSetMuted = jest.fn<Promise<void>, [boolean]>();
const mockRoom = { current: null as string | null };

jest.mock('../../../services/voice/voiceRoom', () => ({
  DisconnectReason: { PARTICIPANT_REMOVED: 4, ROOM_DELETED: 5 },
  VoiceRoom: jest.fn().mockImplementation(() => ({
    connect: async (access: VoiceAccess, muted: boolean) => {
      mockRoom.current = access.room;
      await mockConnect(access, muted);
    },
    disconnect: async () => {
      mockRoom.current = null;
      await mockDisconnect();
    },
    setMuted: (muted: boolean) => mockSetMuted(muted),
    get currentRoom() {
      return mockRoom.current;
    },
  })),
}));

const mockRequestVoiceToken = jest.fn<Promise<VoiceAccess | null>, []>();
jest.mock('../../../services/signalr/proximityHub', () => ({
  proximityHub: { requestVoiceToken: () => mockRequestVoiceToken() },
}));

const mockVoiceTokenApi = jest.fn<Promise<VoiceAccess>, []>();
jest.mock('../../../services/api/endpoints', () => ({
  proximityApi: { voiceToken: () => mockVoiceTokenApi() },
}));

import { useDrivingStore } from '../../../store/drivingStore';
import { useProximityStore } from '../../../store/proximityStore';
import { useVoiceStore } from '../../../store/voiceStore';
import { voiceController } from '../voiceController';

const access = (room: string, expiresInMs = 300_000): VoiceAccess => ({
  url: 'wss://voice.test',
  token: `token-${room}`,
  room,
  identity: 'd-me',
  expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
});

const flush = async () => {
  for (let i = 0; i < 5; i++) {
    await new Promise<void>((resolve) => setImmediate(() => resolve()));
  }
};

const session = { id: 's1', handle: 'd-me', startedAt: new Date().toISOString(), endedAt: null, endReason: null };

describe('voice follows server-side proximity', () => {
  beforeAll(() => voiceController.init());

  beforeEach(async () => {
    useProximityStore.getState().reset();
    useDrivingStore.getState().setEnded(null);
    await voiceController.disconnect();
    jest.clearAllMocks();
  });

  it('proximity joined → connects to exactly the authorised room', async () => {
    useDrivingStore.getState().setActive(session);
    useProximityStore.getState().applyGroupJoined({ groupId: 'g1', members: [], voice: access('opd-g1') });
    await flush();

    expect(mockConnect).toHaveBeenCalledTimes(1);
    expect(mockConnect.mock.calls[0]?.[0].room).toBe('opd-g1');
    expect(mockRequestVoiceToken).not.toHaveBeenCalled();
  });

  it('fetches a fresh token when the pushed one is missing or about to expire', async () => {
    mockRequestVoiceToken.mockResolvedValue(access('opd-g2'));
    useDrivingStore.getState().setActive(session);
    useProximityStore.getState().applyGroupJoined({ groupId: 'g2', members: [], voice: access('opd-g2', 2_000) });
    await flush();

    expect(mockRequestVoiceToken).toHaveBeenCalledTimes(1);
    expect(mockConnect.mock.calls[0]?.[0].token).toBe('token-opd-g2');
  });

  it('proximity left → leaves the voice room', async () => {
    useDrivingStore.getState().setActive(session);
    useProximityStore.getState().applyGroupJoined({ groupId: 'g1', members: [], voice: access('opd-g1') });
    await flush();

    useProximityStore.getState().applyGroupLeft('g1', 'outOfRange');
    await flush();

    expect(mockDisconnect).toHaveBeenCalled();
    expect(mockRoom.current).toBeNull();
  });

  it('switching groups (merge) moves to the new room', async () => {
    useDrivingStore.getState().setActive(session);
    useProximityStore.getState().applyGroupJoined({ groupId: 'g1', members: [], voice: access('opd-g1') });
    await flush();
    useProximityStore.getState().applyGroupJoined({ groupId: 'g3', members: [], voice: access('opd-g3') });
    await flush();

    expect(mockConnect.mock.calls.map((c) => c[0].room)).toEqual(['opd-g1', 'opd-g3']);
    expect(mockRoom.current).toBe('opd-g3');
  });

  it('session ended → voice disconnected', async () => {
    useDrivingStore.getState().setActive(session);
    useProximityStore.getState().applyGroupJoined({ groupId: 'g1', members: [], voice: access('opd-g1') });
    await flush();

    useDrivingStore.getState().setEnded('userEnded');
    await flush();

    expect(mockDisconnect).toHaveBeenCalled();
    expect(mockRoom.current).toBeNull();
  });

  it('never joins voice without an active drive', async () => {
    useProximityStore.getState().applyGroupJoined({ groupId: 'g1', members: [], voice: access('opd-g1') });
    await flush();

    expect(mockConnect).not.toHaveBeenCalled();
  });

  it('applies the start-muted preference when connecting', async () => {
    useVoiceStore.getState().setMuted(true);
    useDrivingStore.getState().setActive(session);
    useProximityStore.getState().applyGroupJoined({ groupId: 'g1', members: [], voice: access('opd-g1') });
    await flush();

    expect(mockConnect.mock.calls[0]?.[1]).toBe(true);
    useVoiceStore.getState().setMuted(false);
  });
});
