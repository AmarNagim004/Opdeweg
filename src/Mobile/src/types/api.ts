// Mirrors the backend DTOs (camelCase JSON, enums as camelCase strings).

export interface Me {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  shareDisplayName: boolean;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: Me;
}

export type DrivingSessionEndReason = 'userEnded' | 'signedOut' | 'idle' | 'replaced' | 'accountDeleted' | 'orphaned';

export interface DrivingSession {
  id: string;
  handle: string;
  startedAt: string;
  endedAt: string | null;
  endReason: DrivingSessionEndReason | null;
}

export interface DrivingSessionEnded {
  sessionId: string;
  reason: DrivingSessionEndReason;
  endedAt: string;
}

export interface LocationUpdate {
  latitude: number;
  longitude: number;
  speed?: number;
  heading?: number;
  accuracy?: number;
  timestamp: string;
}

export interface NearbyDriver {
  /** Per-session pseudonym; also the driver's voice identity. */
  id: string;
  displayName: string;
  avatarUrl: string | null;
  approxDistanceMeters: number | null;
  inVoiceGroup: boolean;
}

export interface VoiceAccess {
  url: string;
  token: string;
  room: string;
  identity: string;
  expiresAt: string;
}

export interface ProximityGroup {
  groupId: string;
  members: NearbyDriver[];
  voice: VoiceAccess | null;
}

export type ProximityChangeReason = 'proximity' | 'outOfRange' | 'merged' | 'groupDissolved' | 'sessionEnded' | 'stale';

export interface ProximityGroupLeft {
  groupId: string;
  reason: ProximityChangeReason;
}

export interface ProximitySnapshot {
  sessionActive: boolean;
  group: ProximityGroup | null;
  nearby: NearbyDriver[];
  nearbyCount: number;
  generatedAt: string;
}

export type LocationUpdateStatus = 'accepted' | 'lowAccuracy' | 'throttled';

export interface LocationUpdateResponse {
  status: LocationUpdateStatus;
  proximity: ProximitySnapshot | null;
}

export interface ClientConfig {
  proximity: { joinDistanceMeters: number; leaveDistanceMeters: number; maxGroupSize: number };
  location: {
    distanceThresholdMeters: number;
    maxIntervalSeconds: number;
    minIntervalMilliseconds: number;
    headingChangeDegrees: number;
    speedChangeMetersPerSecond: number;
  };
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  code?: string;
}
