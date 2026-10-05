import { distanceMeters, headingDifference } from '../../utils/geo';

export interface LocationSample {
  latitude: number;
  longitude: number;
  /** m/s; null when unknown. */
  speed: number | null;
  /** Degrees from north; null when unknown. */
  heading: number | null;
  /** Horizontal accuracy radius in metres. */
  accuracy: number | null;
  /** Fix time (ms since epoch). */
  timestamp: number;
}

export interface ThrottleConfig {
  /** Transmit once the driver moved this far since the last transmitted fix. */
  distanceThresholdMeters: number;
  /** Transmit at least this often (liveness heartbeat). */
  maxIntervalMs: number;
  /** Never transmit more often than this unless forced. */
  minIntervalMs: number;
  headingChangeDegrees: number;
  speedChangeMetersPerSecond: number;
  /** Below this speed headings are noise and are ignored. */
  minSpeedForHeadingMetersPerSecond: number;
}

export const defaultThrottleConfig: ThrottleConfig = {
  distanceThresholdMeters: 50,
  maxIntervalMs: 15_000,
  minIntervalMs: 2_500,
  headingChangeDegrees: 45,
  speedChangeMetersPerSecond: 5,
  minSpeedForHeadingMetersPerSecond: 3,
};

export type TransmitReason =
  | 'first_fix'
  | 'distance'
  | 'max_interval'
  | 'heading'
  | 'speed'
  | 'session_start'
  | 'app_resume'
  | 'network_restored'
  | 'session_recovered';

export type ForcedReason = Extract<TransmitReason, 'session_start' | 'app_resume' | 'network_restored' | 'session_recovered'>;

export type ThrottleDecision = { transmit: true; reason: TransmitReason } | { transmit: false };

const SKIP: ThrottleDecision = { transmit: false };

/**
 * Decides whether a native location callback should be sent to the server. Pure and
 * timer-free: it runs on each native callback (which keeps arriving in the background),
 * so no JavaScript timers are needed for the max-interval heartbeat.
 */
export function decideTransmit(
  last: { sample: LocationSample; sentAt: number } | null,
  current: LocationSample,
  now: number,
  config: ThrottleConfig,
  forced?: ForcedReason,
): ThrottleDecision {
  if (forced) {
    return { transmit: true, reason: forced };
  }

  if (!last) {
    return { transmit: true, reason: 'first_fix' };
  }

  const elapsed = now - last.sentAt;
  if (elapsed < config.minIntervalMs) {
    return SKIP;
  }

  if (elapsed >= config.maxIntervalMs) {
    return { transmit: true, reason: 'max_interval' };
  }

  if (distanceMeters(last.sample, current) >= config.distanceThresholdMeters) {
    return { transmit: true, reason: 'distance' };
  }

  const fastEnough =
    (current.speed ?? 0) >= config.minSpeedForHeadingMetersPerSecond &&
    (last.sample.speed ?? 0) >= config.minSpeedForHeadingMetersPerSecond;
  if (
    fastEnough &&
    current.heading != null &&
    last.sample.heading != null &&
    headingDifference(current.heading, last.sample.heading) >= config.headingChangeDegrees
  ) {
    return { transmit: true, reason: 'heading' };
  }

  if (
    current.speed != null &&
    last.sample.speed != null &&
    Math.abs(current.speed - last.sample.speed) >= config.speedChangeMetersPerSecond
  ) {
    return { transmit: true, reason: 'speed' };
  }

  return SKIP;
}

/** Stateful wrapper remembering the last transmitted fix. */
export class LocationThrottle {
  private last: { sample: LocationSample; sentAt: number } | null = null;

  constructor(private config: ThrottleConfig = defaultThrottleConfig) {}

  evaluate(sample: LocationSample, now: number, forced?: ForcedReason): ThrottleDecision {
    return decideTransmit(this.last, sample, now, this.config, forced);
  }

  markSent(sample: LocationSample, now: number): void {
    this.last = { sample, sentAt: now };
  }

  updateConfig(config: ThrottleConfig): void {
    this.config = config;
  }

  reset(): void {
    this.last = null;
  }
}
