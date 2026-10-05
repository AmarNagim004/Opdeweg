import { create } from 'zustand';

export type VoiceConnection = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'error';
export type MicIssue = 'none' | 'permission' | 'unavailable';
export type AudioOutput = 'auto' | 'speaker';

export interface VoiceParticipant {
  identity: string;
  name: string;
  isSpeaking: boolean;
  isMuted: boolean;
}

interface VoiceState {
  connection: VoiceConnection;
  room: string | null;
  participants: VoiceParticipant[];
  localSpeaking: boolean;
  muted: boolean;
  micIssue: MicIssue;
  output: AudioOutput;
  setConnection: (connection: VoiceConnection, room?: string | null) => void;
  setParticipants: (participants: VoiceParticipant[]) => void;
  setLocalSpeaking: (speaking: boolean) => void;
  setMuted: (muted: boolean) => void;
  setMicIssue: (issue: MicIssue) => void;
  setOutput: (output: AudioOutput) => void;
  reset: () => void;
}

export const useVoiceStore = create<VoiceState>()((set) => ({
  connection: 'idle',
  room: null,
  participants: [],
  localSpeaking: false,
  muted: false,
  micIssue: 'none',
  output: 'auto',
  setConnection: (connection, room) => set((s) => ({ connection, room: room === undefined ? s.room : room })),
  setParticipants: (participants) => set({ participants }),
  setLocalSpeaking: (localSpeaking) => set({ localSpeaking }),
  setMuted: (muted) => set({ muted }),
  setMicIssue: (micIssue) => set({ micIssue }),
  setOutput: (output) => set({ output }),
  reset: () => set({ connection: 'idle', room: null, participants: [], localSpeaking: false, micIssue: 'none' }),
}));
