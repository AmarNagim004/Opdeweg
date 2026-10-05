import { destination } from '../../../utils/geo';
import { decideTransmit, defaultThrottleConfig, LocationThrottle, type LocationSample } from '../locationThrottle';

const origin = { latitude: 52.3791, longitude: 4.9003 };
const T0 = 1_790_000_000_000;

function sample(metersEast: number, overrides: Partial<LocationSample> = {}): LocationSample {
  const point = destination(origin, 90, metersEast);
  return { ...point, speed: 13, heading: 90, accuracy: 6, timestamp: T0, ...overrides };
}

const sentAt = (s: LocationSample, at = T0) => ({ sample: s, sentAt: at });

describe('location throttling', () => {
  const config = defaultThrottleConfig; // 50 m · 15 s · 2.5 s min

  it('transmits the first fix of a session', () => {
    expect(decideTransmit(null, sample(0), T0, config)).toEqual({ transmit: true, reason: 'first_fix' });
  });

  it('does not transmit movement under 50 m', () => {
    expect(decideTransmit(sentAt(sample(0)), sample(49), T0 + 5_000, config)).toEqual({ transmit: false });
  });

  it('transmits movement of 50 m or more', () => {
    expect(decideTransmit(sentAt(sample(0)), sample(50.5), T0 + 5_000, config)).toEqual({ transmit: true, reason: 'distance' });
    expect(decideTransmit(sentAt(sample(0)), sample(120), T0 + 5_000, config)).toEqual({ transmit: true, reason: 'distance' });
  });

  it('transmits once the maximum interval is exceeded, even when stationary', () => {
    expect(decideTransmit(sentAt(sample(0)), sample(0), T0 + 14_999, config)).toEqual({ transmit: false });
    expect(decideTransmit(sentAt(sample(0)), sample(0), T0 + 15_000, config)).toEqual({ transmit: true, reason: 'max_interval' });
  });

  it('never transmits faster than the minimum interval unless forced', () => {
    expect(decideTransmit(sentAt(sample(0)), sample(500), T0 + 1_000, config)).toEqual({ transmit: false });
    expect(decideTransmit(sentAt(sample(0)), sample(0), T0 + 1_000, config, 'app_resume')).toEqual({ transmit: true, reason: 'app_resume' });
  });

  it('transmits on a significant heading change at speed', () => {
    const turned = sample(10, { heading: 180 });
    expect(decideTransmit(sentAt(sample(0)), turned, T0 + 3_000, config)).toEqual({ transmit: true, reason: 'heading' });
  });

  it('ignores heading noise when crawling or stopped', () => {
    const slow = sample(0, { speed: 1, heading: 10 });
    const turned = sample(5, { speed: 1, heading: 200 });
    expect(decideTransmit(sentAt(slow), turned, T0 + 3_000, config)).toEqual({ transmit: false });
  });

  it('transmits on a significant speed change', () => {
    const braking = sample(10, { speed: 4 });
    expect(decideTransmit(sentAt(sample(0)), braking, T0 + 3_000, config)).toEqual({ transmit: true, reason: 'speed' });
  });

  it('honours server-provided thresholds', () => {
    const custom = { ...config, distanceThresholdMeters: 200 };
    expect(decideTransmit(sentAt(sample(0)), sample(150), T0 + 5_000, custom)).toEqual({ transmit: false });
  });

  it('measures distance from the last transmitted fix, not the last callback', () => {
    const throttle = new LocationThrottle(config);
    throttle.markSent(sample(0), T0);
    // Many small steps that individually move < 50 m still add up.
    expect(throttle.evaluate(sample(30), T0 + 3_000)).toEqual({ transmit: false });
    expect(throttle.evaluate(sample(55), T0 + 6_000)).toEqual({ transmit: true, reason: 'distance' });
  });
});
