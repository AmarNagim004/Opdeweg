import { create } from 'zustand';

export type NetworkState = 'unknown' | 'online' | 'offline';
export type RealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

interface ConnectivityState {
  network: NetworkState;
  realtime: RealtimeState;
  backendReachable: boolean;
  setNetwork: (network: NetworkState) => void;
  setRealtime: (realtime: RealtimeState) => void;
  setBackendReachable: (reachable: boolean) => void;
}

export const useConnectivityStore = create<ConnectivityState>()((set) => ({
  network: 'unknown',
  realtime: 'disconnected',
  backendReachable: true,
  setNetwork: (network) => set({ network }),
  setRealtime: (realtime) => set({ realtime }),
  setBackendReachable: (backendReachable) => set({ backendReachable }),
}));
